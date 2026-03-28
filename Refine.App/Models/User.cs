using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Refine.App.Models;

public class User
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Language { get; set; } = "";
    public string Gender { get; set; } = "";
    public double Height { get; set; }
    public double Weight { get; set; }

    [Ignore]
    public List<WorkoutProgram> WorkoutPrograms { get; set; } = new();
}
