using System.Linq;
using UnityEngine;

namespace WardrobeAndTackOverhaul;

internal enum Activity
{
    Training,
    TrailRides,
    Lessons,
    Caprilli,
    Jumping,
    CrossCountry,
}

internal static class Activities
{
    // The Normal Outfit's tabs are these, the competition outfit's the next ones. Lessons are
    // only for tack: a student rides then, the player isn't shown.
    public static readonly Activity[] Everyday = { Activity.Training, Activity.TrailRides };
    public static readonly Activity[] Competition = { Activity.Caprilli, Activity.Jumping, Activity.CrossCountry };
    public static readonly Activity[] All = { Activity.Training, Activity.TrailRides, Activity.Lessons, Activity.Caprilli, Activity.Jumping, Activity.CrossCountry };

    public static string Label(Activity a) => a switch
    {
        Activity.Training => "Training",
        Activity.TrailRides => "Trail rides",
        Activity.Lessons => "Lessons",
        Activity.Caprilli => "Caprilli",
        Activity.Jumping => "Jumping",
        Activity.CrossCountry => "Cross country",
        _ => a.ToString(),
    };

    // What the ride or lesson being set up is; null when it is none of ours (tutorial, unknown).
    public static Activity? Current()
    {
        if (Resources.FindObjectsOfTypeAll<RidingLesson>().Any(l => l.gameObject.activeInHierarchy && l.enabled))
            return Activity.Lessons;

        var manager = CourseManager.i;
        var course = manager?.GetCurrentlySelectedCourse();
        if (course == null)
            return null;
        if (Contains(manager.competitionCourses, course))
            return course.discipline switch
            {
                Discipline.CAPRILLI => Activity.Caprilli,
                Discipline.SHOWJUMPING => Activity.Jumping,
                Discipline.CROSSCOUNTRY => Activity.CrossCountry,
                _ => null,
            };
        if (Contains(manager.trails, course))
            return Activity.TrailRides;
        if (Contains(manager.trainingCourses, course))
            return Activity.Training;
        return null;
    }

    private static bool Contains<T>(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<T> list, T course)
        where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
    {
        if (list == null)
            return false;
        foreach (var c in list)
            if (c != null && c.Pointer == course.Pointer)
                return true;
        return false;
    }

}
