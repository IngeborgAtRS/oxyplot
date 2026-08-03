// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PlotView.cs" company="OxyPlot">
//   Copyright (c) 2014 OxyPlot contributors
// </copyright>
// <summary>
//   Represents a control that displays a <see cref="PlotModel" />.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace OxyPlot.Wpf
{
    using System.Diagnostics;

    using OxyPlot;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;

    /// <summary>
    /// Different modes of rendering the plot. The default is <see cref="Drawing"/>.
    /// </summary>
    public enum RenderMode
    {
        /// <summary>
        /// The graph is rendered as a DrawingGroup.
        /// </summary>
        Drawing,
        /// <summary>
        /// The graph is rendered as a collection of Path and TextBlock elements.
        /// </summary>
        Canvas,
        /// <summary>
        /// Similar to Canvas, but the graph is rendered in a way that allows serializing to XAML.
        /// </summary>
        Xaml,
    }

    /// <summary>
    /// Represents a control that displays a <see cref="PlotModel" />. This <see cref="IPlotView"/> is based on <see cref="DrawingRenderContext"/>.
    /// </summary>
    public partial class PlotView : PlotViewBase
    {
        /// <summary>
        /// Identifies the <see cref="RenderMode"/> dependency property. This controls the type of render surface used.
        /// </summary>
        public static readonly DependencyProperty RenderModeProperty = DependencyProperty.Register(nameof(RenderMode), typeof(RenderMode), typeof(PlotView), new PropertyMetadata(RenderMode.Drawing, OnRenderModeChanged));

        private static void OnRenderModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PlotView plotView)
            {
                plotView.ReplaceRenderSurface();
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="RenderModeProperty"/> dependency property/>.
        /// </summary>
        /// <value>The true if the rendering should create WPF elements.</value>
        public RenderMode RenderMode
        {
            get => (RenderMode)this.GetValue(RenderModeProperty);
            set => this.SetValue(RenderModeProperty, value);
        }

        /// <summary>
        /// Identifies the <see cref="TextMeasurementMethod"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty TextMeasurementMethodProperty =
            DependencyProperty.Register(
                nameof(TextMeasurementMethod), typeof(TextMeasurementMethod), typeof(PlotViewBase), new PropertyMetadata(TextMeasurementMethod.TextBlock));

        /// <summary>
        /// Initializes a new instance of the <see cref="PlotView" /> class.
        /// </summary>
        public PlotView()
        {
            this.DisconnectCanvasWhileUpdating = true;
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, this.DoCopy));
        }

        /// <summary>
        /// Gets or sets a value indicating whether to disconnect the canvas while updating.
        /// </summary>
        /// <value><c>true</c> if canvas should be disconnected while updating; otherwise, <c>false</c>.</value>
        public bool DisconnectCanvasWhileUpdating { get; set; }

        /// <summary>
        /// Gets or sets the text measurement method.
        /// </summary>
        /// <value>The text measurement method.</value>
        public TextMeasurementMethod TextMeasurementMethod
        {
            get => (TextMeasurementMethod)this.GetValue(TextMeasurementMethodProperty);
            set => this.SetValue(TextMeasurementMethodProperty, value);
        }

        /// <summary>
        /// Gets the Canvas.
        /// </summary>
        protected Panel RenderSurface => (Panel)this.plotPresenter;

        /// <summary>
        /// Gets the WpfRenderContext.
        /// </summary>
        private WpfRenderContext RenderContext => this.renderContext as WpfRenderContext;

        /// <inheritdoc/>
        protected override void ClearBackground()
        {
            this.RenderSurface.Children.Clear();

            if (this.ActualModel != null && this.ActualModel.Background.IsVisible())
            {
                this.RenderSurface.Background = this.ActualModel.Background.ToBrush();
            }
            else
            {
                this.RenderSurface.Background = null;
            }
        }

        /// <inheritdoc/>
        protected override FrameworkElement CreatePlotPresenter()
        {
            return this.RenderMode switch
            {
                RenderMode.Drawing => new RenderSurface(),
                RenderMode.Canvas => new Canvas(),
                RenderMode.Xaml => new Canvas(),
                _ => null
            };
        }

        /// <inheritdoc/>
        protected override IRenderContext CreateRenderContext()
        {
            return this.RenderMode switch
            {
                RenderMode.Drawing => new DrawingRenderContext(this.RenderSurface as RenderSurface),
                RenderMode.Canvas => new CanvasRenderContext(this.RenderSurface as Canvas),
                RenderMode.Xaml => new XamlRenderContext(this.RenderSurface as Canvas),
                _ => null
            };
        }

        private void ReplaceRenderSurface()
        {
            if (this.grid == null)
            {
                return;
            }

            this.grid.Children.Remove(this.plotPresenter);
            this.plotPresenter = this.CreatePlotPresenter();
            this.renderContext = this.CreateRenderContext();
            this.grid.Children.Add(this.plotPresenter);
            this.plotPresenter.UpdateLayout();
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            // Note that if the RenderMode is Canvas or Xaml, this will add elements to the visual tree.
            // This is highly questionable, since it will trigger a new measure/arrange cycle, something 
            // that should not be done in the render phase. In some cases this can result in failure to redraw the graph.
            this.Render(); 
            base.OnRender(drawingContext);
        }

        /// <inheritdoc/>
        protected override void RenderOverride()
        {
            this.RenderContext.TextMeasurementMethod = this.TextMeasurementMethod;
            switch (this.RenderMode)
            {
                case RenderMode.Drawing:
                    if (this.RenderSurface is RenderSurface renderSurface)
                    {
                        renderSurface.BeginRender();
                        try
                        {
                            base.RenderOverride();
                        }
                        finally
                        {
                            Debug.Assert(
                                this.renderContext.ClipCount == 0,
                                "Unbalanced clipping stack after rendering.");
                            renderSurface.EndRender();
                        }
                    }

                    break;
                case RenderMode.Canvas:
                case RenderMode.Xaml:
                    if (this.DisconnectCanvasWhileUpdating && this.RenderSurface is Canvas)
                    {
                        // TODO: profile... not sure if this makes any difference
                        var idx = this.grid.Children.IndexOf(this.plotPresenter);
                        if (idx != -1)
                        {
                            this.grid.Children.RemoveAt(idx);
                        }

                        base.RenderOverride();

                        if (idx != -1)
                        {
                            // reinsert the canvas again
                            this.grid.Children.Insert(idx, this.plotPresenter);
                        }
                    }

                    break;
                default:
                    base.RenderOverride();
                    break;
            }
        }

        /// <inheritdoc/>
        protected override double UpdateDpi()
        {
            var scale = base.UpdateDpi();
            if (this.RenderContext != null)
            {
                this.RenderContext.DpiScale = scale;
                var ancestor = this.GetAncestorVisualFromVisualTree(this);
                this.RenderContext.VisualOffset = ancestor != null ? this.TransformToAncestor(ancestor).Transform(default) : default;
            }

            return scale;
        }

        /// <summary>
        /// Performs the copy operation.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.Windows.Input.ExecutedRoutedEventArgs" /> instance containing the event data.</param>
        private void DoCopy(object sender, ExecutedRoutedEventArgs e)
        {
            var exporter = new PngExporter() { Width = (int)this.ActualWidth, Height = (int)this.ActualHeight };
            var bitmap = exporter.ExportToBitmap(this.ActualModel);
            Clipboard.SetImage(bitmap);
        }


        /// <summary>
        /// Returns a reference to the visual object that hosts the dependency object in the visual tree.
        /// </summary>
        /// <returns> The host window from the visual tree.</returns>
        private Visual GetAncestorVisualFromVisualTree(DependencyObject startElement)
        {

            DependencyObject child = startElement;
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                child = parent;
                parent = VisualTreeHelper.GetParent(child);
            }

            return child as Visual ?? Window.GetWindow(this);
        }
    }
}
