using UnityEngine;
using UnityEngine.UI;

namespace JamesFrowen.CSP.Debugging
{
    public class TickDebuggerCanvasGraph : TickDebuggerOutput
    {
        public RectInt Rect;
        public float scale = 5;
        public float thinkness = 20;
        private GraphLine DiffGraph;

        private void Start()
        {
            var gameObject = new GameObject("TickDebuggerCanvasGraph", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer));
            var canvas = gameObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            DiffGraph = new GraphLine(Rect.width, Rect, canvas.transform, "Diff", thinkness, Color.red);
        }

        private void LateUpdate()
        {
            DiffGraph?.AddValue((float)Diff * scale);
        }

        private class GraphLine
        {
            private readonly RectTransform[] dataPoints;
            private int midPoint;

            public GraphLine(int count, RectInt rect, Transform canvas, string name, float thinkness, Color color)
            {
                dataPoints = new RectTransform[count];

                var parent = new GameObject(name, typeof(RectTransform));
                parent.transform.SetParent(canvas, true);

                midPoint = rect.y + rect.height / 2;
                for (var x = 0; x < rect.width; x++)
                {
                    var dataPoint = new GameObject("DataPoint", typeof(RectTransform), typeof(Image));
                    var image = dataPoint.GetComponent<Image>();
                    image.color = color;
                    var rectTransform = dataPoint.GetComponent<RectTransform>();
                    dataPoints[x] = rectTransform;
                    rectTransform.SetParent(parent.transform, true);
                    rectTransform.sizeDelta = new Vector2(1, thinkness);
                    rectTransform.position = new Vector2(rect.x + x, midPoint);
                }
            }

            public void AddValue(float newValue)
            {
                // move all values to left 1 index
                for (var i = 0; i < dataPoints.Length - 1; i++)
                {
                    dataPoints[i].position = new Vector2(dataPoints[i].position.x, dataPoints[i + 1].position.y);
                }

                // set right most index to new data
                dataPoints[dataPoints.Length - 1].position = new Vector2(dataPoints[dataPoints.Length - 1].position.x, newValue + midPoint);
            }
        }
    }
}
