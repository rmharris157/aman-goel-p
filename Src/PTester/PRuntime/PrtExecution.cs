﻿
using System;
using System.Collections.Generic;
using System.Linq;

namespace P.PRuntime
{
    public abstract class PrtFun
    {
        public abstract string Name
        {
            get;
        }

        public abstract List<PrtValue> CreateLocals(params PrtValue[] args);

        public abstract void Execute(PStateImpl application, PrtMachine parent);
    }

    public class PrtEvent
    {
        public static int DefaultMaxInstances = int.MaxValue;
        public static PrtEvent NullEvent = new PrtEvent("Null", PrtType.NullType, DefaultMaxInstances, false);
        public static PrtEvent HaltEvent = new PrtEvent("Halt", PrtType.NullType, DefaultMaxInstances, false);
        

        public string name;
        public PrtType payloadType;
        public int maxInstances;
        public bool doAssume;

        public PrtEvent(string name, PrtType payload, int mInstances, bool doAssume)
        {
            this.name = name;
            this.payloadType = payload;
            this.maxInstances = mInstances;
            this.doAssume = doAssume;
        }
    };

    public class PrtTransition
    {
        public PrtFun transitionFun; // isPush <==> fun == null
        public PrtState gotoState;

        public PrtTransition(PrtFun fun, PrtState toState)
        {
            this.transitionFun = fun;
            this.gotoState = toState;
        }
    };

    public enum StateTemperature
    {
        Cold,
        Warm,
        Hot
    };

    public class PrtState
    {
        public string name;
        public PrtFun entryFun;
        public PrtFun exitFun;
        public Dictionary<PrtEvent, PrtTransition> transitions;
        public Dictionary<PrtEvent, PrtFun> dos;
        public bool hasNullTransition;
        public StateTemperature temperature;
        public HashSet<PrtEvent> deferredSet;

        public PrtState(string name, PrtFun entryFun, PrtFun exitFun, bool hasNullTransition, StateTemperature temperature)
        {
            this.name = name;
            this.entryFun = entryFun;
            this.exitFun = exitFun;
            this.transitions = new Dictionary<PrtEvent, PrtTransition>();
            this.dos = new Dictionary<PrtEvent, PrtFun>();
            this.hasNullTransition = hasNullTransition;
            this.temperature = temperature;
        }

        public PrtTransition FindPushTransition(PrtEvent evt)
        {
            if (transitions.ContainsKey(evt))
            {
                PrtTransition transition = transitions[evt];
                if (transition.transitionFun == null)
                    return transition;
            }
            return null;
        }

        public PrtTransition FindTransition(PrtEvent evt)
        {
            if (transitions.ContainsKey(evt))
            {
                return transitions[evt];
            }
            else
            {
                return null;
            }
        }
    };

    public class PrtEventNode
    {
        public PrtEvent ev;
        public PrtValue arg;

        public PrtEventNode(PrtEvent e, PrtValue payload)
        {
            ev = e;
            arg = payload;
        }
    }

    public class PrtEventBuffer
    {
        List<PrtEventNode> events;
        public PrtEventBuffer()
        {
            events = new List<PrtEventNode>();
        }

        public int Size()
        {
            return events.Count();
        }
        public int CalculateInstances(PrtEvent e)
        {
            return events.Select(en => en.ev).Where(ev => ev == e).Count();
        }

        public void EnqueueEvent(PrtEvent e, PrtValue arg)
        {
            if (e.maxInstances == PrtEvent.DefaultMaxInstances)
            {
                events.Add(new PrtEventNode(e, arg));
            }
            else
            {
                if (CalculateInstances(e) == e.maxInstances)
                {
                    if (e.doAssume)
                    {
                        throw new PrtAssumeFailureException();
                    }
                    else
                    {
                        throw new PrtMaxEventInstancesExceededException(
                            String.Format(@"< Exception > Attempting to enqueue event {0} more than max instance of {1}\n", e.name, e.maxInstances));
                    }
                }
                else
                {
                    events.Add(new PrtEventNode(e, arg));
                }
            }
        }

        public void DequeueEvent(PrtMachine owner)
        {
            HashSet<PrtEvent> deferredSet;
            HashSet<PrtEvent> receiveSet;

            deferredSet = owner.stateStack.deferredSet;
            receiveSet = owner.receiveSet;

            int iter = 0;
            while (iter < events.Count)
            { 
                if ((receiveSet.Count == 0 && !deferredSet.Contains(events[iter].ev))
                    || (receiveSet.Count > 0 && receiveSet.Contains(events[iter].ev)))
                {
                    owner.currentEvent = events[iter].ev;
                    owner.currentArg = events[iter].arg;
                    events.Remove(events[iter]);
                    return;
                }
                else
                {
                    continue;
                }
            }
        }

        public bool IsEnabled(PrtMachine owner)
        {
            HashSet<PrtEvent> deferredSet;
            HashSet<PrtEvent> receiveSet;

            deferredSet = owner.stateStack.deferredSet;
            receiveSet = owner.receiveSet;
            foreach (var evNode in events)
            {
                if ((receiveSet.Count == 0 && !deferredSet.Contains(evNode.ev))
                    || (receiveSet.Count > 0 && receiveSet.Contains(evNode.ev)))
                {
                    return true;
                }

            }
            return false;
        }
    }

    internal class PrtStateStackFrame
    {
        public PrtState state;
        public HashSet<PrtEvent> deferredSet;
        public HashSet<PrtEvent> actionSet;

        public PrtStateStackFrame(PrtState st, HashSet<PrtEvent> defSet, HashSet<PrtEvent> actSet)
        {
            this.state = st;
            this.deferredSet = new HashSet<PrtEvent>();
            foreach (var item in defSet)
                this.deferredSet.Add(item);

            this.actionSet = new HashSet<PrtEvent>();
            foreach (var item in actSet)
                this.actionSet.Add(item);
        }

        public PrtStateStackFrame Clone()
        {
            return new PrtStateStackFrame(state, deferredSet, actionSet);
        }

    }
    
    internal class PrtStateStack
    {
        public PrtStateStack()
        {
            stateStack = new Stack<PrtStateStackFrame>();
        }

        private Stack<PrtStateStackFrame> stateStack;

        public PrtStateStackFrame TopOfStack
        {
            get
            {
                if (stateStack.Count > 0)
                    return stateStack.Peek();
                else
                    return null;
            }
        }
       
        public PrtStateStack Clone()
        {
            var clone = new PrtStateStack();
            foreach(var s in stateStack)
            {
                clone.stateStack.Push(s.Clone());
            }
            clone.stateStack.Reverse();
            return clone;
        }

        public void PopStackFrame()
        {
            stateStack.Pop();
        }


        public void PushStackFrame(PrtState state)
        {
            var deferredSet = new HashSet<PrtEvent>();
            if (TopOfStack != null)
            {
                deferredSet.UnionWith(TopOfStack.deferredSet);
            }
            deferredSet.UnionWith(state.deferredSet);
            deferredSet.ExceptWith(state.dos.Keys);
            deferredSet.ExceptWith(state.transitions.Keys);

            var actionSet = new HashSet<PrtEvent>();
            if (TopOfStack != null)
            {
                actionSet.UnionWith(TopOfStack.actionSet);
            }
            actionSet.ExceptWith(state.deferredSet);
            actionSet.UnionWith(state.dos.Keys);
            actionSet.ExceptWith(state.transitions.Keys);

            //push the new state on stack
            stateStack.Push(new PrtStateStackFrame(state, deferredSet, actionSet));
        }

        public bool HasNullTransitionOrAction()
        {
            if (TopOfStack.state.hasNullTransition) return true;
            return TopOfStack.actionSet.Contains(PrtEvent.NullEvent);
        }
    }


    #region Function Stack
    public enum PrtContinuationReason : int
    {
        Return,
        Nondet,
        Pop,
        Raise,
        Receive,
        Send,
        NewMachine,
    };

    public class PrtFunStackFrame
    {
        public List<PrtValue> locals;
        public PrtContinuation cont;
        public PrtFun fun;
        public PrtFunStackFrame(PrtFun fun,  List<PrtValue> locs)
        {
            this.fun = fun;
            cont = new PrtContinuation();
            locals = locs.ToList();
        }
    }

    public class PrtFunStack
    {
        private Stack<PrtFunStackFrame> funStack;
        public PrtFunStackFrame TopOfStack
        {
            get
            {
                return funStack.Peek();
            }
        }

        public void PushFun(PrtFun fun, List<PrtValue> locals)
        {
            funStack.Push(new PrtFunStackFrame(fun, locals));
        }

        public void PopFun()
        {
            funStack.Pop();
        }

        public void DidReturn(List<PrtValue> retLocals)
        {
            var cont = new PrtContinuation();
            cont.reason = PrtContinuationReason.Return;
            cont.retVal = PrtValue.NullValue;
            cont.retLocals = retLocals;
            TopOfStack.cont = cont;
        }

        public void DidReturnVal(PrtValue val, List<PrtValue> retLocals)
        {
            var cont = new PrtContinuation();
            cont.reason = PrtContinuationReason.Return;
            cont.retVal = val;
            cont.retLocals = retLocals;
            TopOfStack.cont = cont;
        }

        public void DidPop()
        {
            var cont = new PrtContinuation();
            cont.reason = PrtContinuationReason.Pop;
            TopOfStack.cont = cont;
        }

        public void DidRaise()
        {
            var cont = new PrtContinuation();
            cont.reason = PrtContinuationReason.Raise;
            TopOfStack.cont = cont;
        }

        public void DidSend(int ret, List<PrtValue> locals)
        {
            var cont = new PrtContinuation();
            cont.reason = PrtContinuationReason.Send;
            cont.returnTolocation = ret;
            TopOfStack.cont = cont;
            TopOfStack.locals = locals.ToList();
        }

        void DidNewMachine(int ret, List<PrtValue> locals, PrtMachine o)
        {
            var cont = new PrtContinuation();
            cont.reason = PrtContinuationReason.NewMachine;
            cont.createdMachine = o;
            cont.returnTolocation = ret;
            TopOfStack.cont = cont;
            TopOfStack.locals = locals.ToList();
        }

        void DidReceive(int ret, List<PrtValue> locals)
        {
            var cont = new PrtContinuation();
            cont.reason = PrtContinuationReason.Receive;
            cont.returnTolocation = ret;
            TopOfStack.cont = cont;
            TopOfStack.locals = locals.ToList();
        }

        void DidNondet(int ret, List<PrtValue> locals)
        {
            var cont = new PrtContinuation();
            cont.reason = PrtContinuationReason.Nondet;
            cont.returnTolocation = ret;
            TopOfStack.cont = cont;
            TopOfStack.locals = locals.ToList();
        }

    }

    public class PrtContinuation
    {
        public int returnTolocation;
        public PrtContinuationReason reason;
        public PrtMachine createdMachine;
        public PrtValue retVal;
        public List<PrtValue> retLocals;
        // The nondet field is different from the fields above because it is used 
        // by ReentrancyHelper to pass the choice to the nondet choice point.
        // Therefore, nondet should not be reinitialized in this class.
        public bool nondet;

        public PrtContinuation()
        {
            reason = PrtContinuationReason.Return;
            createdMachine = null;
            retVal = null;
            nondet = false;
            retLocals = null;
        }
    }

    #endregion
}