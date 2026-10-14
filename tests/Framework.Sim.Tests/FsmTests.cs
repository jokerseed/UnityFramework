using System;
using System.Collections.Generic;
using Framework.DebugConsole;
using Framework.Fsm;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    public sealed class FsmTests
    {
        sealed class Owner
        {
            public readonly List<string> Trace = new List<string>();
        }

        sealed class Idle : FsmState<Owner>
        {
            protected override void OnEnter(Fsm<Owner> fsm) => fsm.Owner.Trace.Add("enter idle");

            protected override void OnUpdate(Fsm<Owner> fsm, float deltaTime)
            {
                if (fsm.CurrentStateTime >= 1f)
                {
                    ChangeState<Run>(fsm);
                }
            }

            protected override void OnExit(Fsm<Owner> fsm) => fsm.Owner.Trace.Add("exit idle");
        }

        sealed class Run : FsmState<Owner>
        {
            protected override void OnEnter(Fsm<Owner> fsm) => fsm.Owner.Trace.Add("enter run");

            protected override void OnExit(Fsm<Owner> fsm)
            {
                fsm.Owner.Trace.Add("exit run");
                if (fsm.TryGetData("change-in-exit", out bool change) && change)
                {
                    fsm.ChangeState<Idle>();
                }
            }
        }

        [Test]
        public void TransitionsRunExitBeforeEnterAndResetStateTime()
        {
            var owner = new Owner();
            var fsm = new Fsm<Owner>("test", owner, new Idle(), new Run());
            fsm.Start<Idle>();
            fsm.Update(0.5f);
            Assert.IsInstanceOf<Idle>(fsm.CurrentState);
            fsm.Update(0.6f);
            Assert.IsInstanceOf<Run>(fsm.CurrentState);
            Assert.AreEqual(0f, fsm.CurrentStateTime);
            CollectionAssert.AreEqual(new[] { "enter idle", "exit idle", "enter run" }, owner.Trace);
        }

        [Test]
        public void ChangingStateInsideExitThrows()
        {
            var fsm = new Fsm<Owner>("test", new Owner(), new Idle(), new Run());
            fsm.Start<Run>();
            fsm.SetData("change-in-exit", true);
            Assert.Throws<InvalidOperationException>(() => fsm.ChangeState<Idle>());
        }

        [Test]
        public void DuplicateOrUnknownStatesAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new Fsm<Owner>("dup", new Owner(), new Idle(), new Idle()));
            var fsm = new Fsm<Owner>("one", new Owner(), new Idle());
            Assert.Throws<InvalidOperationException>(() => fsm.Start<Run>());
        }

        [Test]
        public void CommandRegistryTokenizesQuotesAndDispatches()
        {
            CollectionAssert.AreEqual(new[] { "give", "item one", "3" }, DebugCommandRegistry.Tokenize("give \"item one\"  3"));
            var registry = new DebugCommandRegistry();
            registry.Register("echo", "repeat args", args => string.Join("|", args));
            Assert.AreEqual("a|b c", registry.Execute("ECHO a \"b c\""));
            Assert.Throws<KeyNotFoundException>(() => registry.Execute("missing"));
            Assert.IsNull(registry.Execute("   "));
        }
    }
}
