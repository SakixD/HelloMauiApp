using HelloMauiApp.Services;
using HelloMauiApp.ViewModels;
using LabelPrinting.Models;

namespace HelloMauiApp;

/// <summary>
/// View des Label-Designers. Die gesamte Vorlagen-/Editor-Logik liegt im <see cref="DesignerViewModel"/>;
/// hier lebt nur, was zwingend View ist: das Zeichnen der Elemente auf dem Canvas, die Gesten
/// (Tap-Auswahl, Drag mit 0,5-mm-Raster) und der Zoom (reine Darstellungsgröße, kein Modell-Zustand).
/// </summary>
public partial class DesignerPage : ContentView, IShellSectionView
{
	static readonly double[] ZoomLevels = [0.5, 0.75, 1.0, 1.5, 2.0, 3.0];

	readonly DesignerViewModel _vm;
	readonly Dictionary<LabelElement, Border> _elementViews = [];
	int _zoomIndex = 2; // 100 %

	double PixelsPerMm => LabelCanvasRenderer.PixelsPerMm * ZoomLevels[_zoomIndex];

	public DesignerPage(DesignerViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _vm = viewModel;

		_vm.CanvasResetRequested += RenderCanvas;
		_vm.ElementAdded += OnElementAdded;
		_vm.ElementRemoved += OnElementRemoved;
		_vm.SelectedElementVisualChanged += OnElementVisualChanged;
		_vm.SelectionChanged += OnSelectionChanged;

		RenderCanvas();
	}

	/// <summary>Einstieg für AppShell/Vorlagenverwaltung („Im Designer öffnen“).</summary>
	public void LoadTemplate(LabelTemplate? template) => _vm.LoadTemplate(template);

	public Task OnActivatedAsync() => _vm.OnActivatedAsync();

	// ---------- Canvas-Aufbau ----------

	void RenderCanvas()
	{
		CanvasLayout.Children.Clear();
		_elementViews.Clear();

		CanvasLayout.WidthRequest = Math.Max(10, _vm.Template.WidthMm * PixelsPerMm);
		CanvasLayout.HeightRequest = Math.Max(10, _vm.Template.HeightMm * PixelsPerMm);

		foreach (var element in _vm.Template.Elements)
			AddElementView(element);

		UpdateSelectionStrokes(_vm.SelectedElement);
	}

	void AddElementView(LabelElement element)
	{
		var border = new Border
		{
			Content = CreateInnerView(element),
			Padding = 2,
			StrokeThickness = 1.5,
			Stroke = UnselectedStroke(),
			StrokeDashArray = [2, 2],
			BackgroundColor = Colors.Transparent,
		};

		AttachGestures(border, element);
		LabelCanvasRenderer.PositionOnCanvas(border, element, PixelsPerMm);
		CanvasLayout.Children.Add(border);
		_elementViews[element] = border;
	}

	View CreateInnerView(LabelElement element) =>
		LabelCanvasRenderer.CreateView(element, bv => bv.ToString(), PixelsPerMm);

	// ---------- VM-Benachrichtigungen ----------

	void OnElementAdded(LabelElement element) => AddElementView(element);

	void OnElementRemoved(LabelElement element)
	{
		if (_elementViews.Remove(element, out var border))
			CanvasLayout.Children.Remove(border);
	}

	void OnElementVisualChanged(LabelElement element)
	{
		if (!_elementViews.TryGetValue(element, out var border))
			return;

		border.Content = CreateInnerView(element);
		LabelCanvasRenderer.PositionOnCanvas(border, element, PixelsPerMm);
	}

	void OnSelectionChanged(LabelElement? selected) => UpdateSelectionStrokes(selected);

	void UpdateSelectionStrokes(LabelElement? selected)
	{
		foreach (var (element, border) in _elementViews)
		{
			bool isSelected = ReferenceEquals(element, selected);
			border.Stroke = isSelected ? new SolidColorBrush(AccentColor()) : UnselectedStroke();
			border.StrokeThickness = isSelected ? 2 : 1.5;
			border.StrokeDashArray = isSelected ? [] : [2, 2];
		}
	}

	static Brush UnselectedStroke() => new SolidColorBrush(Color.FromArgb("#26000000"));

	static Color AccentColor() =>
		Application.Current?.Resources.TryGetValue("AccentColor", out var value) == true && value is Color color
			? color
			: Colors.DodgerBlue;

	// ---------- Gesten (Auswahl + Ziehen mit 0,5-mm-Raster) ----------

	void AttachGestures(Border border, LabelElement element)
	{
		double startXmm = 0, startYmm = 0;

		var pan = new PanGestureRecognizer();
		pan.PanUpdated += (s, e) =>
		{
			if (e.StatusType == GestureStatus.Started)
			{
				startXmm = element.X;
				startYmm = element.Y;
				_vm.SelectElement(element);
			}
			else if (e.StatusType == GestureStatus.Running)
			{
				double ppm = PixelsPerMm;
				double newX = Math.Clamp(startXmm + e.TotalX / ppm, 0, Math.Max(0, _vm.Template.WidthMm - 2));
				double newY = Math.Clamp(startYmm + e.TotalY / ppm, 0, Math.Max(0, _vm.Template.HeightMm - 2));

				// 0,5-mm-Raster: verhindert "krumme" Positionen wie 13,37 mm beim Ziehen.
				newX = Math.Round(newX * 2) / 2;
				newY = Math.Round(newY * 2) / 2;

				_vm.MoveElement(element, newX, newY);
				LabelCanvasRenderer.PositionOnCanvas(border, element, ppm);
			}
		};
		border.GestureRecognizers.Add(pan);

		var tap = new TapGestureRecognizer();
		tap.Tapped += (s, e) => _vm.SelectElement(element);
		border.GestureRecognizers.Add(tap);
	}

	// ---------- Zoom ----------

	void OnZoomOutClicked(object? sender, EventArgs e) => SetZoom(_zoomIndex - 1);

	void OnZoomInClicked(object? sender, EventArgs e) => SetZoom(_zoomIndex + 1);

	void SetZoom(int index)
	{
		int clamped = Math.Clamp(index, 0, ZoomLevels.Length - 1);
		if (clamped == _zoomIndex)
			return;

		_zoomIndex = clamped;
		ZoomLabel.Text = $"{ZoomLevels[_zoomIndex] * 100:0} %";
		RenderCanvas();
	}
}
