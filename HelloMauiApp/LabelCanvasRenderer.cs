using LabelPrinting.Models;
using Microsoft.Maui.Layouts;

namespace HelloMauiApp;

/// <summary>
/// Rendert LabelElement-Objekte als (schematische) MAUI-Views für die Canvas-Anzeige.
/// Wird vom Designer (unaufgelöste Platzhalter, zeigt "{key}") und vom Test-Modus
/// (aufgelöste Testdaten, echte Vorschau) gleichermaßen genutzt.
/// </summary>
public static class LabelCanvasRenderer
{
	public const double PixelsPerMm = 3.0;

	public static View CreateView(LabelElement element, Func<BindableValue, string> resolve) => element switch
	{
		TextElement text => new Label
		{
			Text = resolve(text.Text),
			FontSize = Math.Max(6, text.FontSizeMm * PixelsPerMm),
			TextColor = Colors.Black,
			LineBreakMode = LineBreakMode.NoWrap,
		},
		BarcodeElement barcode => CreateBarcodeView(barcode, resolve),
		ImageElement image => CreateImageView(image),
		FrameElement frame => new Border
		{
			WidthRequest = Math.Max(2, frame.WidthMm * PixelsPerMm),
			HeightRequest = Math.Max(2, frame.HeightMm * PixelsPerMm),
			Stroke = Colors.Black,
			StrokeThickness = frame.Filled ? 0 : Math.Max(1, frame.ThicknessMm * PixelsPerMm),
			BackgroundColor = frame.Filled ? Colors.Black : Colors.Transparent,
		},
		LineElement line => line.Orientation == LineOrientation.Horizontal
			? new BoxView { WidthRequest = Math.Max(2, line.LengthMm * PixelsPerMm), HeightRequest = Math.Max(1, line.ThicknessMm * PixelsPerMm), Color = Colors.Black }
			: new BoxView { WidthRequest = Math.Max(1, line.ThicknessMm * PixelsPerMm), HeightRequest = Math.Max(2, line.LengthMm * PixelsPerMm), Color = Colors.Black },
		_ => new BoxView { WidthRequest = 20, HeightRequest = 20, Color = Colors.Gray },
	};

	public static void PositionOnCanvas(View view, LabelElement element)
	{
		AbsoluteLayout.SetLayoutFlags(view, AbsoluteLayoutFlags.None);
		AbsoluteLayout.SetLayoutBounds(view, new Rect(element.X * PixelsPerMm, element.Y * PixelsPerMm, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
	}

	public static bool IsSymbology2D(BarcodeSymbology symbology) =>
		symbology is BarcodeSymbology.QrCode or BarcodeSymbology.DataMatrix or BarcodeSymbology.Pdf417;

	static string SymbologyShortLabel(BarcodeSymbology symbology) => symbology switch
	{
		BarcodeSymbology.QrCode => "QR",
		BarcodeSymbology.DataMatrix => "DataMatrix",
		BarcodeSymbology.Pdf417 => "PDF417",
		_ => "2D",
	};

	static View CreateBarcodeView(BarcodeElement barcode, Func<BindableValue, string> resolve)
	{
		string displayValue = resolve(barcode.Data);

		if (IsSymbology2D(barcode.Symbology))
		{
			double size = Math.Max(24, barcode.Magnification * 10);
			return new Border
			{
				WidthRequest = size,
				HeightRequest = size,
				Stroke = Colors.Black,
				StrokeThickness = 1,
				Content = new Label
				{
					Text = $"{SymbologyShortLabel(barcode.Symbology)}\n{displayValue}",
					FontSize = 9,
					HorizontalTextAlignment = TextAlignment.Center,
					HorizontalOptions = LayoutOptions.Center,
					VerticalOptions = LayoutOptions.Center,
				},
			};
		}

		double heightPx = Math.Max(20, barcode.HeightMm * PixelsPerMm);
		var bars = new HorizontalStackLayout { Spacing = 1 };
		var rnd = new Random(displayValue.GetHashCode());
		for (int i = 0; i < 30; i++)
		{
			bars.Children.Add(new BoxView
			{
				WidthRequest = rnd.Next(1, 4),
				HeightRequest = heightPx,
				Color = i % 2 == 0 ? Colors.Black : Colors.White,
			});
		}

		var stack = new VerticalStackLayout { Spacing = 2 };
		stack.Children.Add(bars);
		if (barcode.PrintHumanReadable)
			stack.Children.Add(new Label { Text = displayValue, FontSize = 10, HorizontalOptions = LayoutOptions.Center });

		return stack;
	}

	static View CreateImageView(ImageElement image)
	{
		double width = Math.Max(10, image.WidthMm * PixelsPerMm);
		double height = Math.Max(10, image.HeightMm * PixelsPerMm);

		if (string.IsNullOrEmpty(image.ImageBase64))
		{
			return new Border
			{
				WidthRequest = width,
				HeightRequest = height,
				Stroke = Colors.Gray,
				Content = new Label { Text = "Bild", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
			};
		}

		byte[] bytes = Convert.FromBase64String(image.ImageBase64);
		return new Image
		{
			Source = ImageSource.FromStream(() => new MemoryStream(bytes)),
			WidthRequest = width,
			HeightRequest = height,
			Aspect = Aspect.AspectFit,
		};
	}
}
