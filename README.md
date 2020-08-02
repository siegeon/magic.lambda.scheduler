
# Magic Lambda Scheduler

[![Build status](https://travis-ci.org/polterguy/magic.lambda.scheduler.svg?master)](https://travis-ci.org/polterguy/magic.lambda.scheduler)

Provides the ability to create scheduled tasks for [Magic](https://github.com/polterguy.magic). More specifically provides the following slots.

* __[scheduler.tasks.create]__ - Creates a new scheduled task.
* __[scheduler.tasks.get]__ - Returns an existing scheduled task according to its name.
* __[scheduler.tasks.list]__ - Lists all scheduled tasks.
* __[scheduler.tasks.delete]__ - Deletes a named scheduled task.
* __[scheduler.stop]__ - Stops the scheduler, implying all tasks will temporary be paused.
* __[scheduler.start]__ - Starts the scheduler. Notice, depending upon your configuration, this signal might need to be raised in order to actually start processing tasks.
* __[scheduler.running]__ - Returns true if the scheduler is actually running.

When creating a task, you can create a task that only executes once. This is done as follows for instance.

```
scheduler.tasks.create
   title:foo-bar-task
   description:Some foo bar task
   when:date:"2020-12-24T17:00"
   .lambda

      /*
       * Your task's lambda object goes here
       */
      log.info:Executing foo-bar-task
```

The above **[when]** node is a date and time in the future for when you want your task to be scheduled
for evaluation. After the task has been evaluated, it will be removed from your scheduler, and never evaluate again.
The name of your task in the above example becomes _"task-name"_, and the task can be referenced later using this name.
The name must be unique, otherwise any previously created tasks with the same name will be overwritten.

## Repeating tasks

To have a task periodically being evaluated, you can choose between a whole range of repetition patterns. For instance,
to have a task scheduled for evaluation every Sunday at 22:00:00, you could create a task such as the following.

```
scheduler.tasks.create:task-name
   pattern:**.**.22.00.00.sunday
   .lambda

      /* Your tasks lambda object goes here /*
      .foo-something
```

You can choose any combinations of weekdays you wish to have your task repeat on a specific weekday.
These weekdays can be combines with the pipe (|) character. Below is an exhaustive list.

* Sunday
* Monday
* Tuesday
* Wednesday
* Thursday
* Friday
* Saturday

To evaluate a task every saturday and sunday for instance, you can use `saturday|sunday` as your weekday.

Evaluating your task every second/minute/hour can be done by using something such as the following.

```
scheduler.tasks.create:task-name
   pattern:50.seconds
   .lambda

      /* Your tasks lambda object goes here /*
      .foo-something
```

The above will evaluate your task every 50 second. The above _"seconds"_ can be exchanged with _"minutes"_, _"hours"_, _"days"_, _"weeks"_ or _"months"_. Notice, this allows you to have _very large integer values_, to have tasks that
are repeating _very seldom_, such as e.g. the following illustrates.

```
scheduler.tasks.create:task-name
   pattern:3650.days
   .lambda

      /* Your tasks lambda object goes here /*
      .foo-something
```

The above task will only be evaluated every 3650 days, which of course becomes every 10 years, which is hopefully
not a meaningful repetition pattern for you for the record. To create a task that is evaluated on the first day of
the month, at 5PM, you can use the following repetition pattern.

```
scheduler.tasks.create:task-name
   pattern:**.01.05.00.00.**
   .lambda
      /* Your tasks lambda object goes here /*
      .foo-something
```

When supplying hours and minutes such as the above example illustrates, you must use military hours, implying from 00:00 to 23:59. The format of the **[pattern]** argument is as follows `MM.dd.HH.mm.ss.ww`. Where the entities implies the following.

* MM - month
* dd - day
* HH - hours (military hours)
* mm - minutes
* ss - seconds
* ww - weekdays

The following entities can supply multiple values, separated by a pipe character (|).

* MM - months, e.g. `01|02|03` for something that should be done in January, February and March months only.
* dd - days, e.g. `01|15` for something that should be done on the 1st and 15th day of the month only.
* ww - weekdays, e.g. `saturday|sunday` for something that should be done on Saturdays and Sundays only.

The double asterix `**` implies _"any value"_, and means _"undefined"_. If you create a weekday pattern, you must
also supply hours, minutes and seconds. If you create a month/day pattern, you must at least supply a day value,
in addition to hours, minutes and seconds.

**Notice** - All times are interpreted as UTC times, and _not_ necessarily your local time. Have this in mind as you
create your tasks.

**Notice** - You can also create a task _without_ any repetition pattern at all, at which point the task is persisted
into your database, but never executed unless you explicitly choose to execute it. If you wish to do this, you can
completely ommit the **[when]** and the **[pattern]** argument(s).

**Notice** - The task **[title]** can also be supplied as the value of the **[scheduler.tasks.create]** invocation
instead of explicitly supplying a **[title]** argument.
