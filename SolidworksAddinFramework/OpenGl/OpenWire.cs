using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.DoubleNumerics;
using MathNet.Numerics.LinearAlgebra.Double;
using OpenTK.Graphics.OpenGL;
using SolidworksAddinFramework.Geometry;
using SolidWorks.Interop.sldworks;

namespace SolidworksAddinFramework.OpenGl
{
    public abstract class Wire : RendererBase<IReadOnlyList<Vector3>>
    {
        private readonly PrimitiveType _Mode;
        public double Thickness { get; set; }

        public Color Color { get; set; }

        protected Wire(IEnumerable<Vector3> points, double thickness, PrimitiveType mode, Color color):base(points.ToList())
        {
            Thickness = thickness;
            _Mode = mode;
            Color = color;
        }

        protected override IReadOnlyList<Vector3> DoTransform(IReadOnlyList<Vector3> data, Matrix4x4 transform)
        {
            return data.Select(v => Vector3.Transform(v,transform)).ToList();
        }

        protected override void DoRender(IReadOnlyList<Vector3> data, DateTime time, double opacity, bool visibile)
        {
            if (!Visibility)
                return;
            using (ModernOpenGl.SetLineWidth(Thickness))
            {
                using (ModernOpenGl.SetColor(FromArgb(opacity, Color), ShadingModel.Smooth, solidBody:false))
                using (ModernOpenGl.Begin(_Mode))
                {
                    data.ForEach(p=>p.GLVertex3());
                }
            }
        }


        protected override Tuple<Vector3, double> UpdateBoundingSphere(IReadOnlyList<Vector3> data, DateTime time)
        {
            throw new NotImplementedException();
        }
    }

    public class OpenWire : Wire
    {
        public OpenWire(IEnumerable<Vector3> points, double thickness, Color color)
            : base(points, thickness, PrimitiveType.LineStrip, color)
        { }

        public OpenWire(ICurve curve, double thickness, Color color, double chordTol=1e-6, double lengthTol = 0) : this(curve.GetTessPoints(chordTol, lengthTol), (double)thickness, color)
        {
            
        }

        public OpenWire(IEnumerable<double[]> points, double thickness, Color color) : this(points.Select(p=>p.ToVector3D()), thickness, color)
        { }

    }

    public class ClosedWire : Wire
    {
        public ClosedWire(IEnumerable<Vector3> points, double thickness, Color color)
            : base(points, thickness, PrimitiveType.LineLoop, color)
        { }
    }
}