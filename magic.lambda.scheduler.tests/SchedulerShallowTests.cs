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
               new RepetitionPatternFactory("01.01.01");
            });
        }

        [Fact]
        public void InvalidRepetitionPattern_02()
        {
            Assert.Throws<FormatException>(() =>
            {
               new RepetitionPatternFactory("MM.dd.HH.mm.ss");
            });
        }

        [Fact]
        public void InvalidRepetitionPattern_03()
        {
            Assert.Throws<FormatException>(() =>
            {
               new RepetitionPatternFactory("**.**.**.**.ss");
            });
        }

        [Fact]
        public void InvalidRepetitionPattern_04()
        {
            Assert.Throws<ArgumentException>(() =>
            {
               new RepetitionPatternFactory("Monday|Wrongday.01.01.01");
            });
        }

        [Fact]
        public void InvalidRepetitionPattern_05()
        {
            Assert.Throws<ArgumentException>(() =>
            {
               new RepetitionPatternFactory("01.**.**.**.**.Monday");
            });
        }

        [Fact]
        public void InvalidRepetitionPattern_07()
        {
            Assert.Throws<FormatException>(() =>
            {
               new RepetitionPatternFactory("Monday.01.01.**");
            });
        }

        [Fact]
        public void EveryMondayAtMidnight()
        {
            var pattern = new RepetitionPatternFactory("Monday.00.00.00");
            var next = pattern.Next();
            Assert.True(next >= DateTime.UtcNow);
            Assert.Equal(DayOfWeek.Monday, next.DayOfWeek);
            Assert.Equal(0, next.Hour);
            Assert.Equal(0, next.Minute);
            Assert.Equal(0, next.Second);
        }

        [Fact]
        public void WeekdaysPatternToString_01()
        {
            var pattern = new RepetitionPatternFactory("Monday.00.00.00");
            Assert.Equal("Monday.00.00.00", pattern.Pattern);
        }

        [Fact]
        public void WeekdaysPatternToString_02()
        {
            var pattern = new RepetitionPatternFactory("sunday|Monday.23.59.14");
            Assert.Equal("Sunday|Monday.23.59.14", pattern.Pattern);
        }

        [Fact]
        public void MonthPatternToString_01()
        {
            var pattern = new RepetitionPatternFactory("05.05.00.00.00");
            Assert.Equal("05.05.00.00.00", pattern.Pattern);
        }

        [Fact]
        public void EverySaturdayAndSundayAt23_57_01()
        {
            var pattern = new RepetitionPatternFactory("Saturday|Sunday.23.57.01");
            var next = pattern.Next();
            Assert.True(next >= DateTime.UtcNow);
            Assert.True(next.DayOfWeek == DayOfWeek.Saturday || next.DayOfWeek == DayOfWeek.Sunday);
            Assert.Equal(23, next.Hour);
            Assert.Equal(57, next.Minute);
            Assert.Equal(1, next.Second);
        }

        [Fact]
        public void Every5thOfMonth()
        {
            var pattern = new RepetitionPatternFactory("**.05.23.57.01");
            var next = pattern.Next();
            Assert.True(next >= DateTime.UtcNow);
            Assert.Equal(5, next.Day);
            Assert.Equal(23, next.Hour);
            Assert.Equal(57, next.Minute);
            Assert.Equal(1, next.Second);
        }

        [Fact]
        public void Every5thAnd15thOfMonth()
        {
            var pattern = new RepetitionPatternFactory("**.05|15.23.59.59");
            var next = pattern.Next();
            Assert.True(next >= DateTime.UtcNow);
            if (DateTime.UtcNow.Day >= 5 && DateTime.UtcNow.Day <= 16)
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
            var pattern = new RepetitionPatternFactory("01|07.01.00.00.00");
            var next = pattern.Next();
            Assert.True(next >= DateTime.UtcNow);
            if (DateTime.UtcNow.Month >= 1 && DateTime.UtcNow.Month <= 7)
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
            var pattern = new RepetitionPatternFactory("5.seconds");
            var next = pattern.Next();
            Assert.True(next >= DateTime.UtcNow);
            Assert.True((next - DateTime.UtcNow).TotalSeconds >= 4 && (next - DateTime.UtcNow).TotalSeconds < 6);
        }

        [Fact]
        public void Every5Days()
        {
            var pattern = new RepetitionPatternFactory("5.days");
            var next = pattern.Next();
            Assert.True(next >= DateTime.UtcNow);
            Assert.True((next - DateTime.UtcNow).TotalDays >= 4 && (next - DateTime.UtcNow).TotalDays < 6);
        }
    }
}
