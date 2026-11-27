using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// What "on his screen" means for the trial fight (work order 147, the producer's second round: "On his screen must mean SEEN"):
    /// a thing's whole shape inside what the game's camera shows AND clear of the interface's plates: the plate at the top left (her
    /// health and focus, <see cref="InterfacePlate.Whole"/>, when it is drawn) and the skill strip at the bottom (the band work order
    /// 145's warning mark keeps clear of, <see cref="LancerWarningMark.BottomInset"/>), with <see cref="BossTrialValues.SeenMarginPx"/>
    /// to spare. Asked in layout pixels of a view 1080 high (the interface's), from the camera's viewport, so a picture or a window of
    /// any size reads the same. With no camera (nothing to ask) it is seen. Nothing is allocated.
    /// </summary>
    public static class BossTrialScreen
    {
        const float LayoutHeight = 1080f;
        const int Ring = 8;

        /// <summary>
        /// Is a body standing at <paramref name="feet"/>, <paramref name="radius"/> round and <paramref name="height"/> tall, wholly seen:
        /// its foot ring and its top ring all inside the view less the margin, none over the top plate or the strip?
        /// </summary>
        public static bool Seen(Vector3 feet, float radius, float height, float marginPx)
        {
            Camera cam = Camera.main;
            if (cam == null) return true;
            int h = Mathf.Max(1, InterfaceStyle.ScreenHeight), w = Mathf.Max(1, InterfaceStyle.ScreenWidth);
            float width = LayoutHeight * w / h;
            InterfaceValues v = InterfaceStyle.V;
            UiBox plate = v != null && v.TopPlate ? InterfacePlate.Whole(v) : default;
            float bottom = LayoutHeight - LancerWarningMark.BottomInset;
            for (int level = 0; level < 2; level++)
            {
                float y = feet.y + (level == 0 ? 0f : Mathf.Max(0f, height));
                for (int k = 0; k < Ring; k++)
                {
                    float a = k * Mathf.PI * 2f / Ring;
                    var p = new Vector3(feet.x + Mathf.Cos(a) * radius, y, feet.z + Mathf.Sin(a) * radius);
                    Vector3 vp = cam.WorldToViewportPoint(p);
                    if (vp.z <= 0f) return false;
                    float x = vp.x * width, top = (1f - vp.y) * LayoutHeight;
                    if (x < marginPx || x > width - marginPx || top < marginPx || top > bottom - marginPx) return false;
                    if (plate.W > 0f && x < plate.Right + marginPx && top < plate.Y + plate.H + marginPx) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Can the camera see the ground point (and a little over it): no wall, pier or prop between the camera and it? For a seed: not
        /// hidden behind a pier from where his camera looks.
        /// </summary>
        public static bool Unhidden(Vector3 point, float over)
        {
            Camera cam = Camera.main;
            if (cam == null) return true;
            Vector3 to = new Vector3(point.x, point.y + over, point.z), from = cam.transform.position, d = to - from;
            float length = d.magnitude;
            if (length < 1e-3f) return true;
            return !Physics.Raycast(from, d / length, length - 0.2f, ~0, QueryTriggerInteraction.Ignore);
        }
    }
}
