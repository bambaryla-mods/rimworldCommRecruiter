using System;
using System.Diagnostics;
using UnityEngine;
using Verse;

namespace CommRecruiter
{
    // What a button does, so a themed look can colour it; the vanilla look ignores it.
    public enum HireTone
    {
        Neutral,
        Confirm
    }

    // The drawing primitives the hire dialog is built from. This class is the vanilla look. The optional UI Rethemed
    // assembly (Compat/UIRethemed, loaded only with [LFS] UI Rethemed - Core) replaces it with a subclass drawing the
    // same primitives with that mod's widgets; the dialog's layout and logic stay in Dialog_HirePawns.
    public class HireLook
    {
        public static readonly HireLook Vanilla = new HireLook();

        public static HireLook Current { get; private set; } = Vanilla;

        public virtual bool Themed => false;

        // Called by a look assembly once it is ready.
        public static void Use(HireLook look) => Current = look;

        // A themed draw failed: back to vanilla for the rest of the session, with one warning. Only failures from the
        // look itself (its assembly, the theme's, or their code no longer matching) count; other exceptions are bugs in
        // the dialog and are left to the window's own error handling.
        public static void Blame(Exception e)
        {
            if (Current == Vanilla || !FromLook(e)) return;
            Current = Vanilla;
            Log.Warning("[CommRecruiter] The UI Rethemed look failed, the hire window uses the vanilla look from now on: " + e);
        }

        private static bool FromLook(Exception e)
        {
            if (e is TypeLoadException || e is MissingMemberException || e is TypeInitializationException) return true;
            var lookAssembly = Current.GetType().Assembly;
            foreach (StackFrame frame in new StackTrace(e).GetFrames())
            {
                var assembly = frame.GetMethod()?.DeclaringType?.Assembly;
                if (assembly == lookAssembly || assembly?.GetName().Name == "UIRethemedCore") return true;
            }
            return false;
        }

        // Draws the window's frame and title; returns the rect below the title.
        public virtual Rect BeginWindow(Window window, Rect inRect, string title)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 35f), title);
            Text.Font = GameFont.Small;
            return new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);
        }

        public virtual void EndWindow(Window window, Rect inRect)
        {
        }

        // A framed area for a list.
        public virtual void Section(Rect rect) => Widgets.DrawMenuSection(rect);

        // A list row: alternate rows shaded, the selected one marked, hoverable ones lit under the mouse.
        public virtual void Row(Rect rect, bool selected, bool hoverable, bool alternate)
        {
            if (alternate) Widgets.DrawLightHighlight(rect);
            if (selected) Widgets.DrawHighlightSelected(rect);
            else if (hoverable) Widgets.DrawHighlightIfMouseover(rect);
        }

        public virtual void Divider(float x, float y, float width) => Widgets.DrawLineHorizontal(x, y, width);

        public virtual bool Button(Rect rect, string label, bool active = true, HireTone tone = HireTone.Neutral) =>
            Widgets.ButtonText(rect, label, active: active);

        public virtual Color TextDim => Color.gray;
    }
}
