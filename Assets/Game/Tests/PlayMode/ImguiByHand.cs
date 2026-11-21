using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Every OnGUI in the scene called by hand inside an IMGUI container (work order 068): a batch run sends no OnGUI events,
    /// so a test that wants the interface drawn (a picture of it, or the order its pieces are drawn in) runs IMGUI the way
    /// UI Toolkit's IMGUIContainer does outside a window, through the engine's internal GUIUtility.BeginContainer, BeginGUI,
    /// EndGUI and EndContainer (by reflection): a layout pass, then a repaint pass in the pieces' GUI.depth order, the deepest
    /// first (a lower depth is drawn later, over a higher one), as the engine orders them.
    /// </summary>
    public static class ImguiByHand
    {
        /// <summary>One piece drawn: its component and the GUI.depth it set.</summary>
        public struct Drawn
        {
            public MonoBehaviour Piece;
            public int Depth;
            public string Error;
        }

        /// <summary>True when the engine's entry points are there (a newer engine may move them: then the caller is inconclusive).</summary>
        public static bool Available => Find(out _, out _, out _, out _, out _);

        static bool Find(out Type stateType, out MethodInfo beginContainer, out MethodInfo endContainer, out MethodInfo beginGui, out MethodInfo endGui)
        {
            const BindingFlags Any = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            Type gu = typeof(GUIUtility);
            stateType = gu.Assembly.GetType("UnityEngine.ObjectGUIState");
            beginContainer = endContainer = beginGui = endGui = null;
            foreach (MethodInfo m in gu.GetMethods(Any))
            {
                ParameterInfo[] p = m.GetParameters();
                if (m.Name == "BeginContainer" && p.Length == 1 && p[0].ParameterType == stateType) beginContainer = m;
                if (m.Name == "EndContainer" && p.Length == 0) endContainer = m;
                if (m.Name == "BeginGUI" && p.Length == 3) beginGui = m;
                if (m.Name == "EndGUI" && p.Length == 1) endGui = m;
            }
            return stateType != null && beginContainer != null && endContainer != null && beginGui != null && endGui != null;
        }

        /// <summary>
        /// Draws every active OnGUI in the scene, into <paramref name="target"/> when one is given (with a pixel matrix of its
        /// size), else nowhere that is seen (the order alone). Returns the pieces in the order they drew on the repaint.
        /// </summary>
        public static List<Drawn> Draw(RenderTexture target)
        {
            var order = new List<Drawn>();
            if (!Find(out Type stateType, out MethodInfo beginContainer, out MethodInfo endContainer, out MethodInfo beginGui, out MethodInfo endGui)) return order;
            object state = Activator.CreateInstance(stateType, true);
            var targets = new List<(MonoBehaviour b, MethodInfo m, int depth)>();
            foreach (MonoBehaviour b in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (b == null || !b.isActiveAndEnabled) continue;
                MethodInfo m = b.GetType().GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (m != null && m.GetParameters().Length == 0) targets.Add((b, m, 0));
            }
            Event saved = Event.current;
            RenderTexture was = RenderTexture.active;
            // The interface's colours are the palette's sRGB values written as they are, as the engine draws IMGUI to the screen
            // in a linear project; into an sRGB texture with sRGB writes on they were encoded twice (round 1: the red came out
            // pink, 0.14 written as 0.41).
            bool srgbWas = GL.sRGBWrite;
            if (target != null) GL.sRGBWrite = false;
            // The passes: a layout pass, then a repaint into a scratch texture that is never seen (many pieces set their depth
            // only on a repaint, and the engine keeps the depth a piece last set: round 2 drew the enemies' bars over the strip
            // because their depth was read from the layout pass), then the repaint in that order into the target.
            var scratch = new RenderTexture(4, 4, 0, RenderTextureFormat.ARGB32);
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var t = targets[i];
                    beginContainer.Invoke(null, new[] { state });
                    beginGui.Invoke(null, Args(beginGui, 0, 0, 1));
                    Event.current = new Event { type = pass == 0 ? EventType.Layout : EventType.Repaint, mousePosition = new Vector2(-1000f, -1000f) };
                    bool matrix = pass == 2 && target != null;
                    if (pass == 1) RenderTexture.active = scratch;
                    if (matrix)
                    {
                        RenderTexture.active = target;
                        GL.PushMatrix();
                        GL.LoadPixelMatrix(0, target.width, target.height, 0);
                    }
                    string error = null;
                    try
                    {
                        GUI.depth = pass == 0 ? 0 : t.depth;
                        t.m.Invoke(t.b, null);
                        if (pass < 2) targets[i] = (t.b, t.m, GUI.depth);
                    }
                    catch (Exception ex) { error = (ex.InnerException ?? ex).Message; }
                    finally
                    {
                        if (matrix) GL.PopMatrix();
                        try { endGui.Invoke(null, Args(endGui, 1)); } catch (Exception ex) { error = (error ?? "") + " EndGUI: " + (ex.InnerException ?? ex).Message; }
                        endContainer.Invoke(null, null);
                    }
                    if (pass == 2) order.Add(new Drawn { Piece = t.b, Depth = t.depth, Error = error });
                }
                if (pass == 1) targets.Sort((x, y) => y.depth.CompareTo(x.depth)); // a stable order among equals is not promised: neither is the engine's
            }
            Event.current = saved;
            RenderTexture.active = was;
            scratch.Release();
            Object.Destroy(scratch);
            GL.sRGBWrite = srgbWas;
            return order;
        }

        /// <summary>The order as words: each piece's type and depth, and an error where it threw.</summary>
        public static string Describe(List<Drawn> order)
        {
            var names = new List<string>();
            foreach (Drawn d in order) names.Add(d.Piece.GetType().Name + "@" + d.Depth + (d.Error != null ? " threw " + d.Error : ""));
            return string.Join(", ", names);
        }

        /// <summary>The arguments for an internal method: the given whole numbers where it takes whole numbers, a type's default elsewhere.</summary>
        static object[] Args(MethodInfo m, params int[] values)
        {
            ParameterInfo[] p = m.GetParameters();
            var a = new object[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                Type t = p[i].ParameterType;
                int value = i < values.Length ? values[i] : 0;
                if (t == typeof(int)) a[i] = value;
                else if (t == typeof(bool)) a[i] = value != 0;
                else if (t.IsEnum) a[i] = Enum.ToObject(t, value);
                else a[i] = t.IsValueType ? Activator.CreateInstance(t) : null;
            }
            return a;
        }
    }
}
