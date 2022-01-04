using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TCX.Configuration;
using System.IO;

/// <summary>
///Previously, this sample used "LOGGED_IN_QUEUES" DN property of the extension.
///Nowadays(since v16) login/logout state of the agent is explicitly defined as a property of QueueAgent object.
///So, this sample is modified to reflect actual way to work with per queue login for of the extension.
///Old code is left and commented out to provide hints how to modify code which was relying on the value of "LOGGEN_IN_QUEUES" DN property
/// </summary>
namespace OMSamples.Samples
{
    [SampleCode("qlogin")]
    [SampleParam("arg1", "login_all|logout_all|login_current|logout_current|login_only_to|logout_only_from|show_status")]
    [SampleParam("arg2", "agent_extension_number")]
    [SampleParam("arg3...argN", "specified list of the queues where action specified by arg1 should be applied")]
    [SampleWarning("changes login status of the agent in queues")]
    [SampleDescription("shows how to change status of the agent in the queue. ")]
    class QueueLogin : ISample
    {
        //old version
        //private IEnumerable<string> AllAgentQueues(Extension agentdn)
        //{
        //    return agentdn.GetQueues().Select(x => x.Number);

        //}

        private IEnumerable<string> AllAgentQueues(Extension agentdn)
        {
            return agentdn.QueueMembership.Select(x => x.Queue.Number);
        }

        //old code
        //private IEnumerable<string> GetWorkingSet(Extension agentdn)
        //{
        //    return agentdn.GetPropertyValue("LOGGED_IN_QUEUES")?.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries) ?? AllAgentQueues(agentdn);
        //}

        private IEnumerable<string> GetWorkingSet(Extension agentdn)
        {
            return agentdn.QueueMembership.Where(x => x.QueueStatus == QueueStatusType.LoggedIn).Select(x => x.Queue.Number);
            //old code
            //return agentdn.GetPropertyValue("LOGGED_IN_QUEUES")?.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries) ?? AllAgentQueues(agentdn);
        }

        //old code
        //private void ChangeWorkingSet(Extension agentdn, IEnumerable<string> qadd, IEnumerable<string> qremove)
        //{
        //    var res = GetWorkingSet(agentdn).Except(qremove).Union(qadd).Intersect(AllAgentQueues(agentdn));
        //    if (!res.Any() || res.Count() == agentdn.GetQueues().Length) //if no queues are left or all are specified - remove property which selects current queues
        //        agentdn.DeleteProperty("LOGGED_IN_QUEUES");
        //    else
        //        agentdn.SetProperty("LOGGED_IN_QUEUES", string.Join(",", res));
        //    if (!res.Any()) //no any queues, set loging status
        //    {
        //        agentdn.QueueStatus = QueueStatusType.LoggedOut; //set logout if current working set is empty - set logout status (list of current queus was reset above)
        //    }
        //}

        private bool ChangeWorkingSet(Extension agentdn, IEnumerable<string> qadd, IEnumerable<string> qremove)
        {
            var allqueues = AllAgentQueues(agentdn).ToHashSet(); //all assigned queues
            var currentWorkingSet = GetWorkingSet(agentdn).ToHashSet();//all which are currently logged in

            //the set of the QueueAgents which require toggling of QueueStatus
            var to_toggle_status = qadd.ToHashSet().Intersect(allqueues).Except(currentWorkingSet)//those which are not logged in but need to be
                .Union(qremove.ToHashSet().Except(qadd).Intersect(currentWorkingSet))//and those which need to be logged out but currently is logged in.
                .ToHashSet(); //make hashset for fast selection

            foreach(var a in agentdn.QueueMembership.Where(x=>to_toggle_status.Contains(x.Queue.Number)))
                a.QueueStatus = a.QueueStatus == QueueStatusType.LoggedIn ? QueueStatusType.LoggedOut : QueueStatusType.LoggedIn;

            return to_toggle_status.Any();
        }

        //old code
        //private void SetWorkingQueues(Extension agentdn, IEnumerable<string> qadd, IEnumerable<string> qremove, QueueStatusType? force_login_status)
        //{
        //    ChangeWorkingSet(agentdn, qadd, qremove);//change current set of queues. If it will become empty - status of extension will reflect status in all queues.
        //    if (force_login_status.HasValue)
        //    {
        //        agentdn.QueueStatus = force_login_status.Value;
        //    }
        //    agentdn.Save();
        //}

        /// <summary>
        /// modifies working set and optionaly set desired global status.
        /// </summary>
        /// <returns>true if modification are made otherwise = false</returns>
        private bool SetWorkingQueues(Extension agentdn, IEnumerable<string> qadd, IEnumerable<string> qremove, QueueStatusType? force_login_status)
        {
            var global_qstatus_change = force_login_status.HasValue && agentdn.QueueStatus != force_login_status.Value;
            if(global_qstatus_change)
                agentdn.QueueStatus = force_login_status.Value;
            if (ChangeWorkingSet(agentdn, qadd, qremove) || global_qstatus_change)
            {
                agentdn.Save();
                return true;
            }
            return false;
        }


        public void Run(PhoneSystem ps, params string[] args_in)
        {
            var action = args_in[1];
            var agentDN = args_in[2];
            var queues = args_in.Skip(3);
            using (var agent = ps.GetDNByNumber(agentDN) as Extension)
            {
                if (agent == null)
                {
                    Console.WriteLine($"{agentDN} is not a valid extension");
                    return;
                }

                if (!agent.QueueMembership.Any())
                {
                    Console.WriteLine($"Extension {agent.Number} is not an agent of the queues");
                    return;
                }

                switch (action)
                {
                    case "login_all":
                        //login to all queues (reset current set of the queue and set extension status to LoggedIn
                        SetWorkingQueues(agent, AllAgentQueues(agent), new string[0], QueueStatusType.LoggedIn);
                        break;
                    case "logout_all":
                        //oldcode
                        //reset working set to default (all queues) and set status of the extension to LoggedOut
                        //SetWorkingQueues(agent, new string[0], AllAgentQueues(agent), QueueStatusType.LoggedOut);

                        //here is the difference. Logic is stright as it is described above.
                        SetWorkingQueues(agent, AllAgentQueues(agent), new string[0], QueueStatusType.LoggedOut);
                        break;
                    case "login_only_to":
                        //Set status login and specify requested set of the queues to be logged in
                        SetWorkingQueues(agent, queues, AllAgentQueues(agent), QueueStatusType.LoggedIn);
                        break;
                    case "logout_only_from":
                        //remove list of specified queues form current working set.
                        //if set will become empty - state of the extension should be set to LoggedOut
                        SetWorkingQueues(agent, new string[0], queues, null);
                        break;
                    case "login_current":
                        //set status of the extension as logged in. Extension will become logged in to current set of the queues.
                        //please pay attention that current set could be empty.
                        SetWorkingQueues(agent, new string[0], new string[0], QueueStatusType.LoggedIn);
                        break;
                    case "logout_current":
                        //simply change extension status to logged out. Working set of the queues will be left the same.
                        SetWorkingQueues(agent, new string[0], new string[0], QueueStatusType.LoggedOut);
                        break;
                    case "show_login_status":
                        break;
                    default:
                        Console.WriteLine($"Undefined action '{action}'");
                        return;
                }
                //old code
                //Console.WriteLine("Agent {0} {1}:\nWorking set:{2}[forced set {3}]\nInactive Queues:{4}", agentDN, agent.QueueStatus, string.Join(",", GetWorkingSet(agent)), "'" + (agent.GetPropertyValue("LOGGED_IN_QUEUES") ?? "None") + "'", string.Join(",", AllAgentQueues(agent).Except(GetWorkingSet(agent))));


                //here is replacement for old code
                //Extension is logged into specific queue only if:
                //(0)Extension is member of the queue
                //AND
                //(1)Extension.QueueStatus is LoggedIn 
                //AND
                //(2)Extension.CurrentProfile (or Extension.CurrentProfileOverride if active) allows does not force LoggedOut status
                //AND
                //(3)The current status of QueueAgent for the queue is LoggedIn
                //so
                Console.WriteLine($"{agent.Number} - {agent.FirstName} {agent.FirstName}:");
                foreach (var qa in agent.QueueMembership) //(0)
                {
                    var ExtensionStatus = agent.QueueStatus;//(1)
                    var ProfileAllowsLogin = (agent.IsOverrideActiveNow ? agent.CurrentProfileOverride : agent.CurrentProfile)
                        .ForceQueueStatus != (int)QueueStatusType.LoggedOut;//(2)
                    var QueueAgentStatus = qa.QueueStatus;//(3)

                    var cumulativeStatus =
                        ExtensionStatus == QueueStatusType.LoggedIn
                        &&
                        ProfileAllowsLogin
                        &&
                        QueueAgentStatus == QueueStatusType.LoggedIn
                        ?
                        QueueStatusType.LoggedIn
                        :
                        QueueStatusType.LoggedOut;
                    Console.WriteLine($"    Queue {qa.Queue.Number} - {cumulativeStatus} (Extension:{agent.QueueStatus}, ProfileAllows={ProfileAllowsLogin} and QueueAgent:{QueueAgentStatus})");
                }
            }
        }
    }
}