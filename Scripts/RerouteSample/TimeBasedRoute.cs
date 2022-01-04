using System;
using CallFlow;
using TCX.Configuration;
using System.Threading.Tasks;

/// <summary>
/// namespace is ignored
/// </summary>
namespace dummy
{
    //this sample shows how to implement time based routing
    //this script can be deployed on RoutePoint which is not answers the call (with dn property AUTOANSWER set to "0")
    //script checks state of the call and then use Divert(not connected) or Transfer (connected)
    //This script can be executed by other scripts either by MyCall.Call(<RoutePoint>) or MyCall.SwitchTo(<RoutePoint>)
    //please pay attention that the route point is also the DN object and can be used as the destination for the standard call control methods like
    //ReplaceWith (BlindTransfer), Divert, RouteTo etc.
    public class TimeBasedRoute : ScriptBase<TimeBasedRoute>
    {
        //RuleHoursType.SpecificHoursExcludingHolidays does not
        Schedule activeHours = new Schedule(PhoneSystem.Root.GetRuleHourTypeByType(RuleHoursType.SpecificHoursExcludingHolidays))
        //for v16 update 2 can be replaced with simpler version
        //Schedule activeHours = new Schedule(RuleHoursType.SpecificHoursExcludingHolidays)
        {
            {DayOfWeek.Monday, new Schedule.PeriodOfDay(TimeSpan.Parse("8:00"), TimeSpan.Parse("17:00"))},
            {DayOfWeek.Tuesday, new Schedule.PeriodOfDay(TimeSpan.Parse("8:00"), TimeSpan.Parse("17:00"))},
            {DayOfWeek.Wednesday, new Schedule.PeriodOfDay(TimeSpan.Parse("8:00"), TimeSpan.Parse("17:00"))},
            {DayOfWeek.Thursday, new Schedule.PeriodOfDay(TimeSpan.Parse("8:00"), TimeSpan.Parse("17:00"))},
            {DayOfWeek.Friday, new Schedule.PeriodOfDay(TimeSpan.Parse("8:00"), TimeSpan.Parse("17:00"))},
        };
        string MakeSourceAddress(string prefix) //alters
        {
            return $"\"{prefix}:{MyCall.Caller.CallerName}\"<sip:{MyCall.Caller.CallerID}@{MyCall.PS.GetParameterValue("SIPDOMAIN")}>";
        }
        private struct Dest
        {
            public string number;
            public bool vmail;
        }

        private static Dest holiday = new Dest { number =  "8000", vmail = false };
        private static Dest office = new Dest { number = "0001", vmail = false };
        private static Dest out_of_office = new Dest { number = "8001", vmail = true };
        private Dest SelectDestination()
        {
            var isHoliday = ((Tenant)MyCall.PS.GetTenant()).IsHoliday(DateTime.Now);
            var isActiveTime = activeHours.IsActiveTime(DateTime.Now) ?? false;
            //routing decision can take into account holidays, active time of schedule etc.
            //here we decide the same route for holiday and out of office time
            if(isHoliday)
            {
                return holiday;
            }
            else if (isActiveTime)
            {
                return office;
            }
            else
            {
                return out_of_office;
            }

        }
        public override void Start()
        {
            Task.Run(async () =>
            {
                try
                {
                    if (!MyCall.MediaChannelReady) //call is not answered yet then set defau lt destination
                        MyCall.SetDefaultRoute(((PhoneSystem)MyCall.PS).GetParameterValue("OPERATOR"), $"{MyCall.DN.Number} route failed");
                    await MyCall.AssureMedia().ContinueWith(
                    x=>{
                        var dest = SelectDestination();
                        return MyCall.RouteTo(dest.number, MakeSourceAddress($"Altered:"), 30);
                       }
                        ).Unwrap();
                    MyCall.Return(false);
                         
                }
                catch
                {
                    //return will terminate a call. Fallback destination can be specified as
                    MyCall.Return(false);
                }
            }
            );
        }
    }
}