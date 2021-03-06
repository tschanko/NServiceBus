namespace NServiceBus.Core.Tests.Pipeline
{
    using System;
    using NServiceBus.Core.Tests.Features;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Settings;
    using NUnit.Framework;

    [TestFixture,Ignore("Will soon be irrelevant since all behaviors would be pipeline static")]
    class PipelineExecutorTests
    {
        [Test]
        public void Static_behaviors_are_shared_between_executions()
        {
            var builder = new FakeBuilder(typeof(SumBehavior));

            var modifications = new PipelineModifications();
            var settings = new PipelineSettings(modifications);
            settings.Register("Static", typeof(SumBehavior), "A static behavior");
            var executor = new PipelineBase<IncomingContext>(builder, new SettingsHolder(), modifications);

            var ctx1 = new IncomingContext(new RootContext(builder));
            ctx1.Set("Value",2);
            executor.Invoke(ctx1);

            var ctx2 = new IncomingContext(new RootContext(builder));
            ctx2.Set("Value", 3);
            executor.Invoke(ctx2);

            var sum = ctx2.Get<int>("Sum");

            Assert.AreEqual(5, sum);
        }
        
        [Test]
        public void Non_static_behaviors_are_not_shared_between_executions()
        {
            var builder = new FakeBuilder(typeof(SumBehavior));
            var modifications = new PipelineModifications();
            var settings = new PipelineSettings(modifications);
            settings.Register("NonStatic", typeof(SumBehavior), "A non-static behavior");
            var executor = new PipelineBase<IncomingContext>(builder, new SettingsHolder(),  modifications);

            var ctx1 = new IncomingContext(new RootContext(builder));
            ctx1.Set("Value",2);
            executor.Invoke(ctx1);

            var ctx2 = new IncomingContext(new RootContext(builder));
            ctx2.Set("Value", 3);
            executor.Invoke(ctx2);

            var sum = ctx2.Get<int>("Sum");

            Assert.AreEqual(3, sum);
        }

        class SumBehavior : Behavior<IncomingContext>
        {
            int sum;

            public override void Invoke(IncomingContext context, Action next)
            {
                var value = context.Get<int>("Value");
                sum += value;
                context.Set("Sum", sum);
                next();
            }
        }
    }
}