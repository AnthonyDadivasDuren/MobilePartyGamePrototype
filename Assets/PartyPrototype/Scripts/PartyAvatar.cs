using UnityEngine;
using UnityEngine.UI;

namespace PartyPrototype
{
    // Vector portrait placeholder: an unmistakable person icon, never a second name.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PartyAvatar : MaskableGraphic
    {
        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect; var center = r.center;
            float radius = Mathf.Min(r.width, r.height) * .5f;
            Disc(vh, center, radius, radius, color);
            Disc(vh, center, radius * .92f, radius * .92f, new Color32(29,28,42,255));
            Disc(vh, center + new Vector2(0, radius * .22f), radius * .25f, radius * .25f, color);
            Disc(vh, center - new Vector2(0, radius * .35f), radius * .49f, radius * .26f, color);
        }
        static void Disc(VertexHelper vh, Vector2 center, float rx, float ry, Color tint)
        {
            int start = vh.currentVertCount;
            vh.AddVert(center, tint, Vector2.zero);
            for (int i = 0; i <= 64; i++)
            {
                float a = i * Mathf.PI * 2 / 64;
                vh.AddVert(center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry), tint, Vector2.zero);
                if (i > 0) vh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}
