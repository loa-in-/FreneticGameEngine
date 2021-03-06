//
// This file is created by Frenetic LLC.
// This code is Copyright (C) 2017 Frenetic LLC under the terms of a strict license.
// See README.md or LICENSE.txt in the source root for the contents of the license.
// If neither of these are available, assume that neither you nor anyone other than the copyright holder
// hold any right or permission to use this software until such time as the official license is identified.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FreneticGameCore
{
    /// <summary>
    /// Represents an event within the Frenetic Game Engine.
    /// </summary>
    public class FreneticEvent<T> where T: EventArgs
    {
        /// <summary>
        /// All event handlers for this event.
        /// </summary>
        public List<FreneticEventFirer<T>> Handlers = new List<FreneticEventFirer<T>>();

        /// <summary>
        /// Returns whether the <see cref="FreneticEvent{T}"/> has any handlers. If this returns false, firing the event will do nothing.
        /// </summary>
        /// <returns>Whether the event is handled.</returns>
        public bool IsHandled()
        {
            return Handlers.Count > 0;
        }

        /// <summary>
        /// Fire the event with the given arguments.
        /// </summary>
        /// <param name="schedule">The scheduler of relevance.</param>
        /// <param name="args">The arguments.</param>
        public void Fire(Scheduler schedule, T args)
        {
            for (int i = 0; i < Handlers.Count; i++)
            {
                FreneticEventArgs<T> fargs = new FreneticEventArgs<T>()
                {
                    PriorityPosition = Handlers[i].Priority,
                    Context = args,
                    ScheduleHelper = schedule
                };
                Handlers[i].Fire(fargs);
            }
        }

        /// <summary>
        /// Fire the event with the given arguments, with a callback action to indicate delayed completion.
        /// </summary>
        /// <param name="schedule">The scheduler of relevance.</param>
        /// <param name="args">The arguments.</param>
        /// <param name="complete">Action to fire when completed.</param>
        public void Fire(Scheduler schedule, T args, Action complete)
        {
            List<FreneticEventWaiter> fews = new List<FreneticEventWaiter>();
            for (int i = 0; i < Handlers.Count; i++)
            {
                FreneticEventArgs<T> fargs = new FreneticEventArgs<T>()
                {
                    PriorityPosition = Handlers[i].Priority,
                    Context = args,
                    ScheduleHelper = schedule
                };
                FreneticEventWaiter few = Handlers[i].Fire(fargs);
                if (few != null)
                {
                    fews.Add(few);
                }
            }
            if (fews.Count == 0)
            {
                complete();
                return;
            }
            schedule.StartAsyncTask(() =>
            {
                foreach (FreneticEventWaiter few in fews)
                {
                    few.MREFinalComplete.WaitOne();
                }
                schedule.ScheduleSyncTask(complete);
            });
        }

        /// <summary>
        /// Removes all event handlers from a given source.
        /// </summary>
        /// <param name="sourceTracker">The source.</param>
        public void RemoveBySource(Object sourceTracker)
        {
            Handlers.RemoveAll((fef) => fef.SourceTracker.Equals(sourceTracker));
        }

        /// <summary>
        /// Add a non-waitable event handler to this event.
        /// </summary>
        /// <param name="act">The handler.</param>
        /// <param name="sourceTracker">The object sourcing this event (EG a plugin object).</param>
        /// <param name="priority">The priority of the event.</param>
        /// <returns></returns>
        public FreneticEventFirer<T> AddEvent(Action<FreneticEventArgs<T>> act, Object sourceTracker, double priority)
        {
            FreneticEventFirer<T> fefirer = new FreneticEventFirer<T>(act)
            {
                Priority = priority,
                SourceTracker = sourceTracker
            };
            Handlers.Add(fefirer);
            ReSort();
            return fefirer;
        }

        /// <summary>
        /// Add a waitable event handler to this event.
        /// </summary>
        /// <param name="act">The handler.</param>
        /// <param name="sourceTracker">The object sourcing this event (EG a plugin object).</param>
        /// <param name="priority">The priority of the event.</param>
        /// <returns></returns>
        public FreneticEventFirer<T> AddEvent(Action<FreneticEventArgs<T>, FreneticEventWaiter> act, Object sourceTracker, double priority)
        {
            FreneticEventFirer<T> fefirer = new FreneticEventFirer<T>(act)
            {
                Priority = priority,
                SourceTracker = sourceTracker
            };
            Handlers.Add(fefirer);
            ReSort();
            return fefirer;
        }

        /// <summary>
        /// Re-sorts the internal handler list.
        /// </summary>
        public void ReSort()
        {
            Handlers = Handlers.OrderBy((fef) => fef.Priority).ToList();
        }
    }

    /// <summary>
    /// Represents a cancellable event.
    /// </summary>
    public interface ICancellableEvent
    {
        /// <summary>
        /// Gets or sets whether the event is cancelled.
        /// </summary>
        bool Cancelled { get; set; }
    }
    
    /// <summary>
    /// Represents the arguments to an event.
    /// </summary>
    public class FreneticEventArgs<T> where T: EventArgs
    {
        /// <summary>
        /// The priority value of this firing. Not valid after a wait instruction.
        /// </summary>
        public double PriorityPosition;

        /// <summary>
        /// Helper to schedule things within this context.
        /// </summary>
        public Scheduler ScheduleHelper;

        /// <summary>
        /// The contextual arguments to this event.
        /// </summary>
        public T Context;
    }

    /// <summary>
    /// Helper to wait inside events.
    /// </summary>
    public class FreneticEventWaiter : IDisposable
    {
        /// <summary>
        /// The scheduler for this waiter.
        /// </summary>
        public Scheduler Schedule;

        /// <summary>
        /// Whether this waiter has been used to create a wait.
        /// </summary>
        public bool Used = false;

        /// <summary>
        /// The marker for completetion of the waiter.
        /// </summary>
        public ManualResetEvent MRECompletion = new ManualResetEvent(false);

        /// <summary>
        /// Locked until the first pass of the wait run is complete.
        /// </summary>
        public ManualResetEvent MREFirst = new ManualResetEvent(false);

        /// <summary>
        /// Locked until the wait is entirely complete.
        /// </summary>
        public ManualResetEvent MREFinalComplete = new ManualResetEvent(false);

        /// <summary>
        /// Waits for a delay in seconds.
        /// </summary>
        /// <param name="delay">The delay, in seconds.</param>
        public void Wait(double delay)
        {
            Used = true;
            ManualResetEvent mre = new ManualResetEvent(false);
            Schedule.ScheduleSyncTask(() =>
            {
                MRECompletion.Reset();
                mre.Set();
                MRECompletion.WaitOne();
            }, delay);
            MREFirst.Set();
            MRECompletion.Set();
            mre.WaitOne();
        }

        /// <summary>
        /// Waits for an MRE to be set - the runs at the next frame tick.
        /// Waits minimum one frame.
        /// </summary>
        /// <param name="mre"></param>
        public void WaitFor(ManualResetEvent mre)
        {
            Used = true;
            MREFirst.Set();
            MRECompletion.Set();
            mre.WaitOne();
            Wait(0);
        }

        /// <summary>
        /// Whether to automatically complete this waiter at the end of the event system.
        /// If turned to false, must call <see cref="Complete"/> to end the waiter when the event is done.
        /// </summary>
        public bool AutoComplete = true;

        /// <summary>
        /// Marks the Waiter complete. MUST be run if waiting is ever used! Called automatically by the event system.
        /// </summary>
        public void Complete()
        {
            MREFirst.Set();
            MRECompletion.Set();
            MREFinalComplete.Set();
        }

        /// <summary>
        /// Screw microsoft.
        /// </summary>
        /// <param name="disposing">If you input false, screw you.</param>
        protected virtual void Dispose(bool disposing)
        {
            // Useless if check
            if (!disposing)
            {
                return;
            }
            // Actual dispose
            MREFirst.Dispose();
            MRECompletion.Dispose();
            MREFinalComplete.Dispose();
        }

        /// <summary>
        /// Dispose and destroy the event.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }
    }

    /// <summary>
    /// Helper to fire an event.
    /// </summary>
    public class FreneticEventFirer<T> where T: EventArgs
    {
        /// <summary>
        /// Used to fire the event.
        /// </summary>
        public Func<FreneticEventArgs<T>, FreneticEventWaiter> Fire;

        /// <summary>
        /// The priority of this firer.
        /// </summary>
        public double Priority;

        /// <summary>
        /// The object to track this firer's sourcing.
        /// </summary>
        public Object SourceTracker;

        /// <summary>
        /// The action used to fire the FreneticEventArgs.
        /// </summary>
        public Action<FreneticEventArgs<T>> FireAction;

        /// <summary>
        /// The action used to fire the FreneticEventArgs with a waiter.
        /// </summary>
        public Action<FreneticEventArgs<T>, FreneticEventWaiter> FireWaiter;
        
        /// <summary>
        /// Constructs the firer without a waitable option.
        /// </summary>
        /// <param name="noWaitEvent">The standard event.</param>
        public FreneticEventFirer(Action<FreneticEventArgs<T>> noWaitEvent)
        {
            FireAction = noWaitEvent;
            Fire = FireNoWait;
        }

        /// <summary>
        /// Constructs the firer with a waitable option.
        /// </summary>
        /// <param name="waitedEvent">The waiter.</param>
        public FreneticEventFirer(Action<FreneticEventArgs<T>, FreneticEventWaiter> waitedEvent)
        {
            FireWaiter = waitedEvent;
            Fire = FireWait;
        }

        /// <summary>
        /// Fires the event, not returning a waiter.
        /// </summary>
        /// <param name="fea">The arguments to the event.</param>
        /// <returns>No waiter.</returns>
        public FreneticEventWaiter FireNoWait(FreneticEventArgs<T> fea)
        {
            FireAction(fea);
            return null;
        }

        /// <summary>
        /// Fires the event, potentially returning a waiter.
        /// </summary>
        /// <param name="fea">The arguments to the event.</param>
        /// <returns>A waiter if needed.</returns>
        public FreneticEventWaiter FireWait(FreneticEventArgs<T> fea)
        {
            FreneticEventWaiter few = new FreneticEventWaiter() { Schedule = fea.ScheduleHelper };
            fea.ScheduleHelper.StartAsyncTask(() =>
            {
                FireWaiter(fea, few);
                few.MREFirst.Set();
                if (few.Used && few.AutoComplete)
                {
                    few.Complete();
                }
            });
            few.MREFirst.WaitOne();
            if (few.Used)
            {
                return few;
            }
            return null;
        }
    }
}
