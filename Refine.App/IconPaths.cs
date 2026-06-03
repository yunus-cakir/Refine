namespace Refine.App;

public static class IconPaths
{
    // Modern Font Icons (Material Design Icons)
    public const string Home = "\ue88a";
    public const string Workouts = "\ueb43"; // fitness_center
    public const string Analytics = "\uef3e"; // analytics
    public const string More = "\ue241"; // format_list_bulleted
    
    public const string ArrowBack = "\ue5c4"; // arrow_back
    public const string MoreVert = "\ue5d4"; // more_vert

    // Fallbacks to avoid breaking other views that might still reference them
    public const string Workout_Outline = Workouts;
    public const string Workout_Filled = Workouts;
    public const string Program_Outline = "\ue8d2"; // subject / program
    public const string Program_Filled = "\ue8d2";
    public const string Home_Outline = Home;
    public const string Home_Filled = Home;
    public const string Exercise_Outline = "\ue871"; 
    public const string Exercise_Filled = "\ue871";
    public const string Profile_Outline = "\ue7fd"; // person
    public const string Profile_Filled = "\ue7fd";
    
    public const string Analytics_Outline = Analytics;
    public const string Analytics_Filled = Analytics;
    public const string More_Outline = More;
    public const string More_Filled = More;
}