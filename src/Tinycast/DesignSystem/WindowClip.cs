using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Tinycast.DesignSystem;

namespace Tinycast.DesignSystem;

internal static class WindowClip
{
    public static void Round(FrameworkElement element, double radius)
    {
        void Apply()
        {
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
                return;

            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;
            var geometry = compositor.CreateRoundedRectangleGeometry();
            geometry.CornerRadius = new System.Numerics.Vector2((float)radius, (float)radius);
            geometry.Size = new System.Numerics.Vector2((float)element.ActualWidth, (float)element.ActualHeight);
            visual.Clip = compositor.CreateGeometricClip(geometry);
        }

        element.Loaded += (_, _) => Apply();
        element.SizeChanged += (_, _) => Apply();
    }

    public static void RoundPanel(FrameworkElement element) => Round(element, Theme.Radius.Panel);

    public static void RoundDialog(FrameworkElement element) => Round(element, Theme.Radius.Dialog);
}
