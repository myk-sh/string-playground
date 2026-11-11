using UnityEngine;

namespace StringLab
{
    // Lightweight UI plots: predicted partial amplitudes and the actual audio buffer.
    [RequireComponent(typeof(CanvasRenderer))]
    public class LabPlot : UnityEngine.UI.MaskableGraphic
    {
        public bool spectrum;
        public StringSettings settings;
        public float[] samples;
        public float playhead;
        static readonly Color Grid = new Color(.2f, .3f, .32f, .55f);
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float w = r.width, h = r.height;
            for (int i = 0; i <= 4; i++) Line(vh, new Vector2(r.xMin, r.yMin + h * i / 4), new Vector2(r.xMax, r.yMin + h * i / 4), Grid, 1);
            if (spectrum && settings != null)
            {
                for (int n = 1; n <= 16; n++)
                {
                    float x = r.xMin + (n - .5f) * w / 16;
                    float a = (float)System.Math.Abs(StringPhysics.Weight(settings, n));
                    Quad(vh, new Rect(x - w / 55, r.yMin, w / 27.5f, Mathf.Max(2, a * h * .92f)), n == 1 ? new Color(1,.64f,.36f) : color);
                }
            }
            else
            {
                Vector2 prev = new Vector2(r.xMin, r.center.y);
                for (int i = 0; i < 420; i++)
                {
                    float value = 0;
                    if (samples != null)
                    {
                        int index = Mathf.Clamp((int)((playhead + i / 420f * .025f) * StringPhysics.SampleRate), 0, samples.Length - 1);
                        value = samples[index];
                    }
                    Vector2 p = new Vector2(r.xMin + i / 419f * w, r.center.y + value * h * .85f);
                    if (i > 0) Line(vh, prev, p, color, 1.8f);
                    prev = p;
                }
            }
        }
        static void Quad(UnityEngine.UI.VertexHelper vh, Rect r, Color c)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin,r.yMin),c,Vector2.zero); vh.AddVert(new Vector3(r.xMin,r.yMax),c,Vector2.zero);
            vh.AddVert(new Vector3(r.xMax,r.yMax),c,Vector2.zero); vh.AddVert(new Vector3(r.xMax,r.yMin),c,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
        }
        static void Line(UnityEngine.UI.VertexHelper vh, Vector2 a, Vector2 b, Color c, float width)
        {
            Vector2 normal = new Vector2(-(b-a).y, (b-a).x).normalized * width * .5f;
            int start = vh.currentVertCount;
            vh.AddVert(a-normal,c,Vector2.zero); vh.AddVert(a+normal,c,Vector2.zero);
            vh.AddVert(b+normal,c,Vector2.zero); vh.AddVert(b-normal,c,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
        }
    }
}
