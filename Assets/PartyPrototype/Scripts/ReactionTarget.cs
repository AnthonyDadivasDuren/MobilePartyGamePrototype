using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PartyPrototype
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ReactionTarget : MaskableGraphic, ICanvasRaycastFilter, IPointerDownHandler
    {
        public bool Running;
        public Action<int> Result;
        public string Status = "Get ready";
        System.Random random;
        double started = -1, greenAt, nextHop;
        float delay;
        Vector2 moveFrom, moveTo;
        double moveStart;
        bool green, done;
        public void Initialize(int seed)
        { random = new System.Random(seed); delay = 3 + (float)random.NextDouble() * 7; color = new Color32(233,60,73,255); }
        void Update()
        {
            if (!Running || done) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (started < 0) { started = now; nextHop = now; }
            if (!green && now - started >= delay)
            { green = true; greenAt = now; color = new Color32(115,216,139,255); Status = "GREEN — TAP THE CIRCLE!"; }
            if (!green && now >= nextHop)
            {
                var area = ((RectTransform)transform.parent).rect;
                float x = Mathf.Max(0, area.width * .5f - rectTransform.rect.width * .5f - 12);
                float y = Mathf.Max(0, area.height * .5f - rectTransform.rect.height * .5f - 12);
                moveFrom = rectTransform.anchoredPosition;
                moveTo = new Vector2(((float)random.NextDouble()*2-1)*x, ((float)random.NextDouble()*2-1)*y);
                moveStart = now; nextHop = now + .65; Status = "RED — WAIT";
            }
            if (!green) rectTransform.anchoredPosition = Vector2.Lerp(moveFrom, moveTo, Mathf.SmoothStep(0, 1, (float)((now - moveStart) / .65)));
        }
        public bool IsRaycastLocationValid(Vector2 point, Camera camera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, point, camera, out var local)) return false;
            return (local - rectTransform.rect.center).sqrMagnitude <= Mathf.Pow(Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f, 2);
        }
        public void OnPointerDown(PointerEventData data)
        {
            if (!Running || done || started < 0 || data.button != PointerEventData.InputButton.Left) return;
            done = true; raycastTarget = false;
            int ms = green ? (int)Math.Round((Time.realtimeSinceStartupAsDouble - greenAt)*1000) : -1;
            Status = green ? PartyRules.ReactionSeconds(ms) + " — finished!" : "Too early — maximum penalty";
            Result?.Invoke(ms);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var center = rectTransform.rect.center;
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height)*.5f;
            vh.AddVert(center, color, Vector2.zero);
            for (int i=0; i<=64; i++)
            { float angle = i*Mathf.PI*2/64; vh.AddVert(center + new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius, color, Vector2.zero); if(i>0) vh.AddTriangle(0,i,i+1); }
        }
    }
}
