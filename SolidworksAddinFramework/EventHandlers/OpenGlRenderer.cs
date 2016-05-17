using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using SolidworksAddinFramework.Events;
using SolidworksAddinFramework.OpenGl;
using SolidWorks.Interop.sldworks;


namespace SolidworksAddinFramework
{
    public class OpenGlRenderer : IDisposable
    {

        public static ConcurrentDictionary<IModelDoc2, OpenGlRenderer> Lookup = 
            new ConcurrentDictionary<IModelDoc2, OpenGlRenderer>();

        private static int _isInitialized;

        public static IDisposable Setup(SldWorks swApp)
        {
            if (Interlocked.CompareExchange(ref _isInitialized, 1, 0) == 1) return Disposable.Empty;

            var d0 = swApp
                .DoWithOpenDoc(modelDoc =>
                {
                    var modelView = (ModelView) modelDoc.GetFirstModelView();
                    Lookup.GetOrAdd(modelDoc, mv => new OpenGlRenderer(modelView));

                    return Disposable.Create(() =>
                    {
                        OpenGlRenderer openGlRenderer;
                        if (Lookup.TryRemove(modelDoc, out openGlRenderer))
                        {
                            openGlRenderer.Dispose();
                        }
                    });
                });

            var d1 = Disposable.Create(() =>
            {
                foreach (var modelView in Lookup.Keys)
                {
                    OpenGlRenderer openGlRenderer;
                    Lookup.TryRemove(modelView, out openGlRenderer);
                    openGlRenderer.Dispose();
                }
                Debug.Assert(Lookup.IsEmpty);
            });

            return new CompositeDisposable(d0, d1);
        }

        public static IDisposable DisplayUndoable(IRenderable body, IModelDoc2 doc, int layer = 0)
        {
            OpenGlRenderer openGlRenderer;
            if (Lookup.TryGetValue(doc, out openGlRenderer))
            {
                return openGlRenderer.DisplayUndoableImpl(body, doc, layer);
            }
            throw new Exception("Can't render OpenGL content, because the model view wasn't setup properly.");
        }

        private readonly ModelView _MView;

        private readonly IDisposable _Disposable;

        private OpenGlRenderer(ModelView mv)
        {
            _MView = mv;

            DoSetup();
            _Disposable = _MView.BufferSwapNotifyObservable().Subscribe(args =>
            {
                var time = DateTime.Now;
                var layers =
                    BodiesToRenderPrimary.GroupBy(o => o.Value.Item1)
                        .OrderBy(o => o.Key)
                        .Select(o => new {Index = o.Key, Renderables = o.Select(q => q.Value.Item2).ToList()})
                        .ToList();
                int i = 0;
                foreach (var layer in layers)
                {
                    // Clear the depth buffer after each subsequent layer. This
                    // will ensure that they are drawn on top of each other.
                    if(layer.Index!=0)
                        GL.Clear(ClearBufferMask.DepthBufferBit);
                    foreach (var r in layer.Renderables)
                        r.Render(time);
                }
            });
        }

        private ImmutableDictionary<IRenderable, Tuple<int, IRenderable>> BodiesToRender
        {
            set
            {
                if (_DeferRedraw)
                    BodiesToRenderBacking = value;
                else
                    BodiesToRenderPrimary = value;
            }
            get
            {
                return _DeferRedraw ? BodiesToRenderBacking: BodiesToRenderPrimary;
            }
        }

        public ImmutableDictionary<IRenderable, Tuple<int, IRenderable>> BodiesToRenderPrimary { get; private set; } =
            ImmutableDictionary<IRenderable, Tuple<int, IRenderable>>.Empty;

        public ImmutableDictionary<IRenderable, Tuple<int, IRenderable>> BodiesToRenderBacking { get; private set; } =
            ImmutableDictionary<IRenderable, Tuple<int, IRenderable>>.Empty;

        private IDisposable DisplayUndoableImpl(IRenderable body, IModelDoc2 doc, int layer)
        {
            BodiesToRender = BodiesToRender.SetItem(body, Tuple.Create(layer, body));
            Redraw(doc);

            return Disposable.Create(() =>
            {
                var btr = BodiesToRender;
                if(btr.ContainsKey(body))
                    BodiesToRender = btr.Remove(body);
                Redraw(doc);
            });
        }

        private static IModelView Redraw(IModelDoc2 doc)
        {
            var activeView = (IModelView) doc.ActiveView;
            if (!_DeferRedraw)
                activeView.GraphicsRedraw(null);
            return activeView;
        }

        private void DoSetup()
        {
            ////_MView.InitializeShading();
            //var windowHandle = (IntPtr) _MView.GetViewHWndx64();
            _MView.UpdateAllGraphicsLayers = true;
            _MView.InitializeShading();
            //Toolkit.Init();
            //var windowInfo = Utilities.CreateWindowsWindowInfo(windowHandle);
            ////var context = new GraphicsContext(GraphicsMode.Default, windowInfo);
            //var contextHandle = new ContextHandle(windowHandle);
            //var context = new GraphicsContext(contextHandle, Wgl.GetProcAddress, () => contextHandle);
            //context.MakeCurrent(windowInfo);
            //context.LoadAll();

            // TODO some resources are not disposed here.
            // Really we should call `var tk = Toolkit.Init();` here (that's what `GLControl.ctor` does)
            // and then dispose `tk`.
            using (var ctrl = new GLControl())
            using (ctrl.CreateGraphics())
            {
            }
            //Toolkit.Init();
            //IGraphicsContext context = new GraphicsContext(
            //    new ContextHandle(windowHandle),null );
            //context.LoadAll();
        }

        public void Dispose()
        {
            _Disposable.Dispose();
        }

        private static bool _DeferRedraw = false;
        public static IDisposable DeferRedraw(IModelDoc2 doc)
        {
            if (!OpenGlRenderer.Lookup.ContainsKey(doc))
                return Disposable.Empty;

            var renderer = OpenGlRenderer.Lookup[doc];
            renderer.BodiesToRenderBacking = renderer.BodiesToRenderPrimary;
            _DeferRedraw = true;
            return Disposable.Create(() =>
            {
                var activeView = (IModelView)doc.ActiveView;
                renderer.BodiesToRenderPrimary= renderer.BodiesToRenderBacking;
                _DeferRedraw = false;
                activeView.GraphicsRedraw(null);
            });
        }
    }
}
