
# Magic Lambda Tasks and Task Scheduler

[![Build status](https://travis-ci.org/polterguy/magic.lambda.scheduler.svg?master)](https://travis-ci.org/polterguy/magic.lambda.scheduler)

This project provides the ability to create persisted, and/or scheduled Hyperlambda tasks,
for [Magic](https://github.com/polterguy.magic). More specifically it provides the following slots.

* __[wait.tasks.create]__ - Creates a new task.
* __[wait.tasks.get]__ - Returns an existing task.
* __[wait.tasks.list]__ - Lists all tasks.
* __[wait.tasks.execute]__ - Executes an existing task.
* __[wait.tasks.delete]__ - Deletes a task.
* __[wait.scheduler.stop]__ - Stops the scheduler, implying no tasks will be executed at the scheduled time.
* __[wait.scheduler.start]__ - Starts the scheduler.
* __[wait.scheduler.next]__ - Returns the date and time of the next scheduled task, if any.
* __[scheduler.running]__ - Returns true if the scheduler is running.

Notice, all of these slots that starts with **[wait.]** are `async` in nature, and can only be executed
from an async context.

## Creating a task

To create a task without an execution date and no repetition pattern, you can use something such as the following.

```
wait.tasks.create:foo-bar-task-1
   .lambda

      /*
       * Your task's lambda object goes here
       */
      log.info:Executing foo-bar-task-1
```

The name or ID of your task in the above example becomes _"foo-bar-task-1"_, and the task can be referenced later
using this name. The name must be unique, otherwise any previously created tasks with the same name will be silently
overwritten. A task can also optionally have a **[description]** argument, which is a humanly friendly written
description, describing what your task does. Below is an example.

```
wait.tasks.create:foo-bar-task-2
   description:This task will do a little bit of foo and some bar afterwards.
   .lambda
      log.info:Executing foo-bar-task-2
```

**Notice** - Your task's **[id]** argument, can only contain alpha numeric characters, 
a-z, 0-9 - In addition to the special characters `.`, `-` and `_`.

## Executing a task

You can explicitly execute a persisted task at will by invoking **[wait.tasks.execute]**, and passing
in the ID of your task. Below is an example, that assumes you have created the above _"foo-bar-task-1"_ task
first.

```
wait.tasks.execute:foo-bar-task-1
```

## Workflows and the Magic Task Scheduler

The above allows you to persist a _"function invocation"_ for later to execute it, once some specified condition
occurs - Effectively giving you the most important features from Microsoft Workflow Foundation, without the
ridiculous XML and WYSIWYG parts from MWF - In addition to that this also is a .Net Core library, contrary
to MWF.

This allows you to create and persist a function _invocation_, for then to later execute it, as some condition occurs,
arguably giving you _"workflow capabilities"_ in your projects.

**Notice** - By creating your own `ISlot` implementation, you can easily create your own C# classes that are Magic
Signals, allowing you to persist an invocation to your method/class - For then to later execute this method as some
condition occurs. Refer to the [documentation for Magic Lambda](https://github.com/polterguy/magic.lambda) to see how this
is done, and more specifically the _"Extending Hyperlambda"_ section.

## Scheduled tasks

If you want to create a _scheduled_ task, you can choose to have the task execute once in the future, at a specified
date and time, by applying a **[due]** argument.

```
wait.tasks.create:foo-bar-task-3
   due:date:"2020-12-24T17:00"
   .lambda
      log.info:Executing foo-bar-task-3
```

The above **[due]** argument is a UTC date and time in the future for when you want your task to be scheduled
for execution. After the task has been executed, it will never execute again, unless you manually execute it,
or assign a **[repeats]** pattern to it by invoking the slot that schedules existing tasks.

**Notice** - You _cannot_ create a task with a due date being in the past, and all dates are assumed to be in
the UTC timezone.

### Repeating tasks

There are 3 basic **[repeats]** patterns for the Magic Lambda Scheduler, in addition to that you can extend
it with your own parametrized repeating `IPattern` implementation. The built in versions however, are as follows.

* `x.units` - Units can be one of _"seconds"_, _"minutes"_, _"hours"_, _"days"_, _"weeks"_ or _"months"_ - And
`x` can be any integer value.
* `[MM|MM..].[dd|dd..].HH.mm.ss` - Where the entities are in sequence months, days in month, hour, minute and second.
* `[ww|ww..].HH.mm.ss` - Where the entities are weekdays, hour, minute and second.

Notice, month, day of month, and weekdays can have double asterix (\*\*) as their values, implying _"whatever value"_.
MM, dd and ww can also have multiple values, separated by the pipe character (|), to provide multiple values for these entities. See esamples of this further below in this documentation.

### Intervals

Evaluating your task every second/minute/hour can be done by using something such as the following.

```
wait.tasks.create:task-id
   repeats:50.seconds
   .lambda
      log.info:Executing repeating task
```

The above will evaluate your task every 50 second. The above _"seconds"_ can be exchanged with _"minutes"_, _"hours"_, _"days"_, _"weeks"_ or _"months"_. Notice, this allows you to have very large values, to have tasks that are
repeating _very rarely_, such as the following illustrates.

```
wait.tasks.create:task-id
   repeats:3650.days
   .lambda
      log.info:Executing seldomly repeating task once every 10 year
```

The above task will only be evaluated every 3650 days, which becomes once every 10 years.

### Periodically scheduled tasks

To create a task that is executed on the first day of _every_ month, at 5PM, you can use the following
repetition pattern.

```
wait.tasks.create:task-id
   repeats:**.01.05.00.00
   .lambda
      log.info:It is the 1st of the month, any month, and the time is 5AM at night.
```

Hours must be supplied as _"military hours"_, implying from 00:00 to 23:59, where for instance 22 equals 10PM UTC time.
Also notice how we provided a double asterix (\*\*) for the month parts, implying _"any month"_. We could also have provided
multiple days, and/or months, such as the following illustrates, that will create a task that is executed in January and
February, but only on the 5th and 15th of these months.

```
wait.tasks.create:task-id
   repeats:01|02.5|15.05.00.00
   .lambda
      log.info:It is the 5th of 15th of January or February, and the time is 5AM at night.
```

By using the double asterix for month and day of month, you can create a task that is executed _every_ day, at
some specific time of the day (UTC time). Below is an example.

```
wait.tasks.create:task-id
   repeats:**.**.22.00.00
   .lambda
      log.info:It is the 10PM now.
```

### Weekdays pattern

If you use the weekdays pattern, you can create any combinations of weekdays, allowing you to supply multiple
weekdays in a single repetition pattern. Below is an exhaustive list of all possible weekdays.

* Sunday
* Monday
* Tuesday
* Wednesday
* Thursday
* Friday
* Saturday

To evaluate a task every Saturday and Sunday for instance, you can use `saturday|sunday` as your weekday.
Below is an example. Notice, weekdays are case insensitive.

```
wait.tasks.create:task-id
   repeats:saturday|SUNDAY.22.00.00
   .lambda
      log.info:Executing repeating task
```

You can also provide a double asterix (\*\*) for the weekdays pattern, implying _"all days of the week"_.

### Example [repeats] patterns

* Every Monday at 10PM UTC `monday.22.00.00`
* Every Monday through Friday at 10AM UTC `monday|tuesday|wednesday|thursday|friday.10.00.00`
* Every 1st of every month at 5AM UTC `**.01.05.00.00`
* Every January the 5th at midnight `01.05.00.00.00`
* Every January and July the 15th at midnight `01|07.15.00.00.00`

In addition, each task can have multiple (infinite) number of schedules, allowing you to create any amount of
complexity you wish for when to repeat a task. If a task is not successfully executed during its due date, a
log entry will be created, supplying the exception, and the ID of your task. The log entry will be of type _"error"_.

#### Internals

A background thread will be used for executing scheduled tasks, and only _one_ background thread - Which implies
that no tasks will ever be executing in parallel, to avoid thread starvation, due to logical errors in your schedules.
All tasks are executed asynchronously, implying the execution thread will be released back to the operating system,
as the thread is waiting for IO data, from socket connections, etc - Assuming you use the async slots where relevant.

When a repeating task has finished executed, the next due date for the task's execution will be calculated using
its interval pattern - Implying that if you use a 5 second pattern, the schedule for its next execution, will be
calculated 5 seconds from when the task _finished_ executing, which might not necessarily imply that your tasks
are executed exactly every 5 seconds, depending upon how much CPU time your task requires to execute. The interval
pattern declares how many units to count to before executing the task again, from when the task _finished_ executing.

## Deleting a task

Use the **[wait.tasks.delete]** signal to delete a task. This will also delete all future schedules for your task.
An example can be found below.

```
wait.tasks.delete:task-id
```

Besides from the task ID, the delete task signal doesn't take any arguments.

## Inspecting a task

To inspect a task you can use the following.

```
wait.tasks.get:task-id
```

Besides from the task ID, the get task signal doesn't take any arguments. Using this signal, will return the
task's due date(s) in addition to the actualy task.

## Listing tasks

To list tasks, you can use the **[wait.tasks.list]** signal. This slot optionally
handles an **[offset]** and a **[limit]** argument, allowing you to page, which might be
useful if you have a lot of tasks in your system. If no **[limit]** is specified, this signal
will only return the first 10 tasks, including the task's Hyperlambda, but not its repetition
pattern, or due date. Below is an example.

```
wait.tasks.list
   offset:20
   limit:10
```

## Miscelaneous slots

The **[wait.scheduler.stop]** will stop the scheduler, meaning no repeating tasks or tasks with a due date in
the future will execute. Notice, if you create a new task with a due date, and/or a repetition pattern,
the scheduler will automatically start again, unless you create the task setting its **[auto-start]**
argument explicitly to false. When you start the scheduler again, using for instance **[wait.scheduler.start]**,
all tasks will automatically resume, and tasks that have due dates in the past, will immediately start executing.

To determine if the task scheduler is running or not, you can invoke **[scheduler.running]**, which will
return `true` if the scheduler is running. Notice, if you have no scheduled tasks, it will always
return false. And regardless of whether or not the scheduler is running or not, you can always explicitly
execute a task by using **[wait.tasks.execute]**.

To return the date and time for the next scheduled task, you can raise the **[wait.scheduler.next]** signal.

## Persisting tasks

All tasks are persisted into your selected database type of choice, either MySQL or Microsoft SQL Server.
Which implies that even if the server is stopped, all scheduled tasks and normal tasks will automatically
load up again, and be available to the scheduler as the server is restarted. This _might_ imply that
all tasks in the past are immediately executed, which is important for you to understand.

Tasks are by default persisted into your `magic.tasks` table, and schedules are persisted into your
`magic.task_due` table.

## License

Although most of Magic's source code is Open Source, you will need a license key to use it.
[You can obtain a license key here](https://servergardens.com/buy/).
Notice, 7 days after you put Magic into production, it will stop working, unless you have a valid
license for it.

* [Get licensed](https://servergardens.com/buy/)
