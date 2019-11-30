
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
scheduler.tasks.create:task-name
   when:date:"2019-12-24T17:00"
   .lambda

      /*
       * Your task's lambda object goes here
       */
      .foo-something
```

The above **[when]** node is a date and time in the future for when you want your task to be scheduled
for evaluation. After the task has been evaluated, it will be removed from your scheduler, and never evaluate again.
The name of your task in the above example becomes _"task-name"_, and the task can be referenced later using this name.
The name must be unique, otherwise any previously created tasks with the same name will be overwritten.

## Hyperlambda task declarations

The whole idea with the Magic Scheduler is that it allows you to declare your tasks dynamically, passing in Hyperlambda
as your task's declaration. Hyperlambda of course, is Turing Complete, which allows you to _dynamically_ declare your
tasks, without requiring recompilation or restart of your server. This gives you a highly dynamic and agile environment
through which you can declare your tasks, according to your business needs and requirements.

## Repeating tasks

To have a task periodically being evaluated, you can choose between a whole range of repetition patterns. For instance,
to have a task scheduled for evaluation every Sunday at 22:00, you could create a task such as the following.

```
scheduler.tasks.create:task-name
   repeat:Sunday
      time:"22:00"
   .lambda
      /* Your tasks lambda object goes here /*
      .foo-something
```

You can choose any weekday you wish to have your task repeat on a specific weekday. Below is an exhaustive list.

* Sunday
* Monday
* Tuesday
* Wednesday
* Thursday
* Friday
* Saturday

Evaluating your task every seconds/minutes/hours can be done by using any of the following repeating patterns.

```
scheduler.tasks.create:task-name
   repeat:seconds
      value:50
   .lambda
      /* Your tasks lambda object goes here /*
      .foo-something
```

The above will evaluate your task every 50 second. The above _"seconds"_ can be exchanged with _"minutes"_, _"hours"_ or _"days"_.
Notice, this allows you to have _very large integer values_, to have tasks that are repeating _very seldom_, such as e.g. the
following illustrates.

```
scheduler.tasks.create:task-name
   repeat:days
      value:3650
   .lambda
      /* Your tasks lambda object goes here /*
      .foo-something
```

The above task will only be evaluated every 3650 days, which of course becomes every 10 years, which is hopefully not a meaningful
repetition pattern for you for the record. To create a task that is evaluated on the last day of the month, at 5PM, you can use the following
repetition pattern.

```
scheduler.tasks.create:task-name
   repeat:last-day-of-month
      time:"05:00"
   .lambda
      /* Your tasks lambda object goes here /*
      .foo-something
```

When supplying hours and minutes such as the above example illustrates, you must use military hours, implying from 00:00 to 23:59.

If you provide an integer value between 1 and 28 as your **[repeat]** value, the scheduler will interpret this as the day of the month,
and evaluate your task on this particular day of the month, at the specified **[time]**. Below is an example.

```
scheduler.tasks.create:task-name
   repeat:5
      time:"23:55"
   .lambda
      /* Your tasks lambda object goes here /*
      .foo-something
```

The above will evaluate your task every 5th of the month, at 23:55 hours.

**Notice** - All times are interpreted as UTC times, and _not_ necessarily your local time. Have this in mind as you
create your tasks.

## Internals

Internally the scheduler will create one `System.Threading.Timer` for each task in your system, but it will
not exhaust your server's resources, since only one interrupt is internally kept by the operating system, for the first
task in chronological order, and it only allows a configurable amount of threads to execute simultaneously,
with a `SemaphoreSlim`, preventing more than x number of tasks to execute simultaneously, depending upon your
configuration settings, or how you instantiated the scheduler. In addition, no tasks are re-scheduled before after
having been executed, implying that regardless of what small amount of repetition pattern you create for your tasks,
the same job will never executed on two different threads simultaneously.

All access to the internal task list is synchronized with a `ReaderWriterLockSlim`, allowing multiple readers entrance
at the same time, but only one writer, making the scheduler highly optimized for having many repeated tasks,
executing simultaneously.

In addition, the thing is _"async to the core"_, implying no threads will ever be spent waiting much for other threads
to finish, but rather returned to the thread pool immediately, for then to be _"reanimated"_ as they are given access to
the shared resource.
