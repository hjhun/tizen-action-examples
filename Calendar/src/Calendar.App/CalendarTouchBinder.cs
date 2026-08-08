using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace Calendar.App;

internal static class CalendarTouchBinder
{
    public static void Bind(View view, Action activate)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(activate);

        var gesture = new CalendarTouchActivation();
        view.LeaveRequired = true;
        view.TouchEvent += (_, eventArgs) =>
        {
            var state = eventArgs.Touch.GetState(0);
            if (state is PointStateType.Down or PointStateType.Started)
            {
                FocusManager.Instance.SetCurrentFocusView(view);
                gesture.PointerDown();
            }
            else if (state is PointStateType.Up or PointStateType.Finished)
            {
                if (gesture.PointerUp(isInside: true))
                {
                    activate();
                }
            }
            else if (state is PointStateType.Leave or PointStateType.Interrupted)
            {
                gesture.PointerUp(isInside: false);
            }

            return true;
        };
    }
}
