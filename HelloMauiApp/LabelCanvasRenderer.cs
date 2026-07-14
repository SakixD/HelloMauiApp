using LabelPrinting.Models;
using Microsoft.Maui.Layouts;

namespace HelloMauiApp;

/// <summary>
/// Rendert LabelElement-Objekte als (schematische) MAUI-Views für die Canvas-Anzeige.
/// Wird vom Designer (unaufgelöste Platzhalter, zeigt "{key}") und vom Test-Modus
/// (aufgelöste Testdaten, echte Vorschau) gleichermaßen genutzt.
/// Über den optionalen pixelsPerMm-Parameter kann der Designer zoomen (Basis: <see cref="PixelsPerMm"/>).
/// </summary>
public static class LabelCanvasRenderer
{
	public const double PixelsPerMm = 3.0;

	public static View CreateView(LabelElement element, Func<BindableValue, string> resolve, double pixelsPerMm = PixelsPerMm) => element switch
	{
		TextElement text => CreateTextView(text, resolve, pixelsPerMm),
		BarcodeElement barcode => CreateBarcodeView(barcode, resolve, pixelsPerMm),
		ImageElement image => CreateImageView(image, pixelsPerMm),
		FrameElement frame => new Border
		{
			WidthRequest = Math.Max(2, frame.WidthMm * pixelsPerMm),
			HeightRequest = Math.Max(2, frame.HeightMm * pixelsPerMm),
			Stroke = Colors.Black,
			StrokeThickness = frame.Filled ? 0 : Math.Max(1, frame.ThicknessMm * pixelsPerMm),
			BackgroundColor = frame.Filled ? Colors.Black : Colors.Transparent,
		},
		EllipseElement ellipse => new Microsoft.Maui.Controls.Shapes.Ellipse
		{
			WidthRequest = Math.Max(2, ellipse.WidthMm * pixelsPerMm),
			HeightRequest = Math.Max(2, ellipse.HeightMm * pixelsPerMm),
			Stroke = new SolidColorBrush(Colors.Black),
			StrokeThickness = ellipse.Filled ? 0 : Math.Max(1, ellipse.ThicknessMm * pixelsPerMm),
			Fill = ellipse.Filled ? new SolidColorBrush(Colors.Black) : null,
		},
		LineElement line => line.Orientation == LineOrientation.Horizontal
			? new BoxView { WidthRequest = Math.Max(2, line.LengthMm * pixelsPerMm), HeightRequest = Math.Max(1, line.ThicknessMm * pixelsPerMm), Color = Colors.Black }
			: new BoxView { WidthRequest = Math.Max(1, line.ThicknessMm * pixelsPerMm), HeightRequest = Math.Max(2, line.LengthMm * pixelsPerMm), Color = Colors.Black },
		_ => new BoxView { WidthRequest = 20, HeightRequest = 20, Color = Colors.Gray },
	};

	public static void PositionOnCanvas(View view, LabelElement element, double pixelsPerMm = PixelsPerMm)
	{
		AbsoluteLayout.SetLayoutFlags(view, AbsoluteLayoutFlags.None);
		AbsoluteLayout.SetLayoutBounds(view, new Rect(element.X * pixelsPerMm, element.Y * pixelsPerMm, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
	}

	public static bool IsSymbology2D(BarcodeSymbology symbology) =>
		symbology is BarcodeSymbology.QrCode or BarcodeSymbology.DataMatrix or BarcodeSymbology.Pdf417;

	static View CreateTextView(TextElement text, Func<BindableValue, string> resolve, double pixelsPerMm)
	{
		var label = new Label
		{
			Text = resolve(text.Text),
			FontSize = Math.Max(6, text.FontSizeMm * pixelsPerMm),
			TextColor = Colors.Black,
			LineBreakMode = LineBreakMode.NoWrap,
		};

		// Näherung an die ZPL-Rotation (^A..R/I/B dreht im Uhrzeigersinn um den Feld-Ursprung):
		// Anker auf die linke obere Ecke legen, damit das Element an seiner X/Y-Position "hängt".
		if (text.Rotation != TextRotation.None)
		{
			label.AnchorX = 0;
			label.AnchorY = 0;
			label.Rotation = text.Rotation switch
			{
				TextRotation.Rotate90 => 90,
				TextRotation.Rotate180 => 180,
				_ => 270,
			};
		}

		return label;
	}

	static string SymbologyShortLabel(BarcodeSymbology symbology) => symbology switch
	{
		BarcodeSymbology.QrCode => "QR",
		BarcodeSymbology.DataMatrix => "DataMatrix",
		BarcodeSymbology.Pdf417 => "PDF417",
		_ => "2D",
	};

	static View CreateBarcodeView(BarcodeElement barcode, Func<BindableValue, string> resolve, double pixelsPerMm)
	{
		string displayValue = resolve(barcode.Data);
		double scale = pixelsPerMm / PixelsPerMm;

		if (IsSymbology2D(barcode.Symbology))
		{
			double size = Math.Max(24, barcode.Magnification * 10) * scale;
			return new Border
			{
				WidthRequest = size,
				HeightRequest = size,
				Stroke = Colors.Black,
				StrokeThickness = 1,
				Content = new Label
				{
					Text = $"{SymbologyShortLabel(barcode.Symbology)}\n{displayValue}",
					FontSize = 9 * scale,
					HorizontalTextAlignment = TextAlignment.Center,
					HorizontalOptions = LayoutOptions.Center,
					VerticalOptions = LayoutOptions.Center,
				},
			};
		}

		double heightPx = Math.Max(20, barcode.HeightMm * pixelsPerMm);
		var bars = new HorizontalStackLayout { Spacing = 1 };
		var rnd = new Random(displayValue.GetHashCode());
		for (int i = 0; i < 30; i++)
		{
			bars.Children.Add(new BoxView
			{
				WidthRequest = rnd.Next(1, 4) * scale,
				HeightRequest = heightPx,
				Color = i % 2 == 0 ? Colors.Black : Colors.White,
			});
		}

		var stack = new VerticalStackLayout { Spacing = 2 };
		stack.Children.Add(bars);
		if (barcode.PrintHumanReadable)
			stack.Children.Add(new Label { Text = displayValue, FontSize = 10 * scale, TextColor = Colors.Black, HorizontalOptions = LayoutOptions.Center });

		return stack;
	}

	static View CreateImageView(ImageElement image, double pixelsPerMm)
	{
		double width = Math.Max(10, image.WidthMm * pixelsPerMm);
		double height = Math.Max(10, image.HeightMm * pixelsPerMm);

		if (string.IsNullOrEmpty(image.ImageBase64))
		{
			return new Border
			{
				WidthRequest = width,
				HeightRequest = height,
				Stroke = Colors.Gray,
				Content = new Label { Text = "Bild", TextColor = Colors.Gray, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
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
