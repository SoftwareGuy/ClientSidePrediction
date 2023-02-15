using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace JamesFrowen.CSP.Debugging
{
    public static class AfterImageHelper
    {
        private static readonly Queue<Renderer> afterImagePool = new Queue<Renderer>();

        public static void CreateAfterImage(Vector3 position, Color color)
        {
            Renderer afterImage;
            if (afterImagePool.Count != 0)
            {
                afterImage = afterImagePool.Dequeue();
            }
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                afterImage = go.GetComponent<Renderer>();
                var collider = go.GetComponent<Collider>();
                collider.enabled = false;
                GameObject.Destroy(collider);
            }

            afterImage.material.color = color;
            afterImage.transform.position = position;
            HideAsync(afterImage).Forget();
        }

        private static async UniTask HideAsync(Renderer afterImage, float seconds = 1)
        {
            await UniTask.Delay((int)(seconds * 1000));
            afterImagePool.Enqueue(afterImage);
        }
    }
}
