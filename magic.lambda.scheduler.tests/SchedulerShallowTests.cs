// #define DEEP_TESTING
/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using Xunit;
using magic.lambda.scheduler.utilities;
using System;

namespace magic.lambda.scheduler.tests
{
    public class SchedulerShallowTests
    {
        [Fact]
        public void InvalidRepetitionPattern_01()
        {
            Assert.Throws<ArgumentException>(() =>
            {
               new RepetitionPattern("**.**.**.**.**");
            });
        }

        [Fact]
        public void InvalidRepetitionPattern_02()
        {
            Assert.Throws<FormatException>(() =>
            {
               new RepetitionPattern("MM.dd.HH.mm.ss.ww");
            });
        }

        [Fact]
        public void InvalidRepetitionPattern_03()
        {
            Assert.Throws<FormatException>(() =>
            {
               new RepetitionPattern("**.**.**.**.ss.**");
            });
        }

        [Fact]
        public void InvalidRepetitionPattern_04()
        {
            Assert.Throws<ArgumentException>(() =>
            {
               new RepetitionPattern("**.**.**.**.**.Monday|Wrongday");
            });
        }

        [Fact]
        public void InvalidRepetitionPattern_05()
        {
            Assert.Throws<ArgumentException>(() =>
            {
               new RepetitionPattern("01.**.**.**.**.Monday");
            });
        }

        [Fact]
        public void InvalidRepetitionPattern_06()
        {
            Assert.Throws<ArgumentException>(() =>
            {
               new RepetitionPattern("**.01.**.**.**.Monday");
            });
        }

        [Fact]
        public void InvalidRepetitionPattern_07()
        {
            Assert.Throws<ArgumentException>(() =>
            {
               new RepetitionPattern("**.**.**.**.**.Monday");
            });
        }

        [Fact]
        public void InvalidRepetitionPattern_08()
        {
            Assert.Throws<ArgumentException>(() =>
            {
               new RepetitionPattern("**.01.00.00.00.Monday");
            });
        }

        [Fact]
        public void InvalidRepetitionPattern_09()
        {
            Assert.Throws<ArgumentException>(() =>
            {
               new RepetitionPattern("01.**.00.00.00.Monday");
            });
        }

        [Fact]
        public void InvalidRepetitionPattern_10()
        {
            Assert.Throws<ArgumentException>(() =>
            {
               new RepetitionPattern("01.01.**.00.00.**");
            });
        }

        [Fact]
        public void InvalidRepetitionPattern_11()
        {
            Assert.Throws<ArgumentException>(() =>
            {
               new RepetitionPattern("01.01.00.**.00.**");
            });
        }

        [Fact]
        public void InvalidRepetitionPattern_12()
        {
            Assert.Throws<ArgumentException>(() =>
            {
               new RepetitionPattern("01.01.00.00.**.**");
            });
        }

        [Fact]
        public void EveryMondayAtMidnight()
        {
            var pattern = new RepetitionPattern("**.**.00.00.00.Monday");
            var next = pattern.Next();
            Assert.True(next >= DateTime.Now);
            Assert.Equal(DayOfWeek.Monday, next.DayOfWeek);
            Assert.Equal(0, next.Hour);
            Assert.Equal(0, next.Minute);
            Assert.Equal(0, next.Second);
        }

        [Fact]
        public void EverySaturdayAndSundayAt23_57_01()
        {
            var pattern = new RepetitionPattern("**.**.23.57.01.Saturday|Sunday");
            var next = pattern.Next();
            Assert.True(next >= DateTime.Now);
            Assert.True(next.DayOfWeek == DayOfWeek.Saturday || next.DayOfWeek == DayOfWeek.Sunday);
            Assert.Equal(23, next.Hour);
            Assert.Equal(57, next.Minute);
            Assert.Equal(1, next.Second);
        }

        [Fact]
        public void Every5thOfMonth()
        {
            var pattern = new RepetitionPattern("**.05.23.57.01.**");
            var next = pattern.Next();
            Assert.True(next >= DateTime.Now);
            Assert.Equal(5, next.Day);
            Assert.Equal(23, next.Hour);
            Assert.Equal(57, next.Minute);
            Assert.Equal(1, next.Second);
        }

        [Fact]
        public void Every5thAnd15thOfMonth()
        {
            var pattern = new RepetitionPattern("**.05|15.23.59.59.**");
            var next = pattern.Next();
            Assert.True(next >= DateTime.Now);
            if (DateTime.Now.Day >= 5 && DateTime.Now.Day <= 16)
                Assert.Equal(15, next.Day);
            else
                Assert.Equal(5, next.Day);
            Assert.Equal(23, next.Hour);
            Assert.Equal(59, next.Minute);
            Assert.Equal(59, next.Second);
        }

        [Fact]
        public void EveryJanuaryAndJuly()
        {
            var pattern = new RepetitionPattern("01|07.01.00.00.00.**");
            var next = pattern.Next();
            Assert.True(next >= DateTime.Now);
            if (DateTime.Now.Month >= 1 && DateTime.Now.Month <= 7)
                Assert.Equal(7, next.Month);
            else
                Assert.Equal(1, next.Month);
            Assert.Equal(00, next.Hour);
            Assert.Equal(00, next.Minute);
            Assert.Equal(00, next.Second);
        }

        [Fact]
        public void Every5Seconds()
        {
            var pattern = new RepetitionPattern("5.seconds");
            var next = pattern.Next();
            Assert.True(next >= DateTime.Now);
            Assert.True((next - DateTime.Now).TotalSeconds >= 4 && (next - DateTime.Now).TotalSeconds < 6);
        }

        [Fact]
        public void Every5Days()
        {
            var pattern = new RepetitionPattern("5.days");
            var next = pattern.Next();
            Assert.True(next >= DateTime.Now);
            Assert.True((next - DateTime.Now).TotalDays >= 4 && (next - DateTime.Now).TotalDays < 6);
        }
    }
}
