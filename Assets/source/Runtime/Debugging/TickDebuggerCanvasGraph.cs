using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace JamesFrowen.CSP.Debugging
{
    public class TickDebuggerCanvasGraph : TickDebuggerOutput
    {
        public RectInt Rect;
        public float scale = 5;
        [FormerlySerializedAs("thinkness")]
        public float thickness = 20;
        private GraphLine _diffGraph;

        private void Start()
        {
            var gameObject = new GameObject("TickDebuggerCanvasGraph", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer));
            var canvas = gameObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            _diffGraph = new GraphLine(Rect.width, Rect, canvas.transform, "Diff", thickness, Color.red);
        }

        private void LateUpdate()
        {
            _diffGraph?.AddValue((float)Diff * scale);
        }

        private sealed class GraphLine
        {
            private readonly RectTransform[] _dataPoints;
            private readonly int _midPoint;

            public GraphLine(int count, RectInt rect, Transform canvas, string name, float thickness, Color color)
            {
                _dataPoints = new RectTransform[count];

                var parent = new GameObject(name, typeof(RectTransform));
                parent.transform.SetParent(canvas, true);

                _midPoint = rect.y + (rect.height / 2);
                for (var x = 0; x < rect.width; x++)
                {
                    var dataPoint = new GameObject("DataPoint", typeof(RectTransform), typeof(Image));
                    var image = dataPoint.GetComponent<Image>();
                    image.color = color;
                    var rectTransform = dataPoint.GetComponent<RectTransform>();
                    _dataPoints[x] = rectTransform;
                    rectTransform.SetParent(parent.transform, true);
                    rectTransform.sizeDelta = new Vector2(1, thickness);
                    rectTransform.position = new Vector2(rect.x + x, _midPoint);
                }
            }

            public void AddValue(float newValue)
            {
                // move all values to left 1 index
                for (var i = 0; i < _dataPoints.Length - 1; i++)
                {
                    _dataPoints[i].position = new Vector2(_dataPoints[i].position.x, _dataPoints[i + 1].position.y);
                }

                // set right most index to new data
                _dataPoints[_dataPoints.Length - 1].position = new Vector2(_dataPoints[_dataPoints.Length - 1].position.x, newValue + _midPoint);
            }
        }
    }
}
