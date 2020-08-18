
# Magic Lambda Tasks and Task Scheduler

[![Build status](https://travis-ci.org/polterguy/magic.lambda.scheduler.svg?master)](https://travis-ci.org/polterguy/magic.lambda.scheduler)

This project provides the ability to create persisted, and/or scheduled Hyperlambda tasks,
for [Magic](https://github.com/polterguy.magic). More specifically it provides the following slots.

* __[wait.tasks.create]__ - Creates a new task.
* __[wait.tasks.execute]__ - Executes an existing task.
* __[wait.tasks.delete]__ - Deletes a task.
* __[wait.tasks.get]__ - Returns an existing task.
* __[wait.tasks.list]__ - Lists all tasks.
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
using this name. The name must be unique, otherwise any previously created tasks with the same name will be overwritten.
A task can also optionally have a **[description]** argument, which is a humanly friendly written description, describing
what your task does. Below is an example.

```
wait.tasks.create:foo-bar-task-2
   description:This task will do a little bit of foo and some bar afterwards.
   .lambda
      log.info:Executing foo-bar-task-2
```

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
ridiculous XML and WYSIWYG parts from MWF.

This allows you to create and persist some functionality, for then to later execute it, as some condition occurs,
giving you workflow capabilities in your projects.

**Notice** - By creating your own `ISlot` implementation, you can easily create your own C# classes that are Magic
Signals, allowing you to persist an invocation to your method/class - For then to later execute this method as some
condition occurs. Refer to the [main documentation for Magic](https://github.com/polterguy/magic) to see how this
is done.

## Scheduled tasks

If you want to create a _scheduled_ task, you can choose to have the task execute once in the future, at a specified
date and time, by applying a **[due]** argument.

```
wait.tasks.create:foo-bar-task-3
   due:date:"2020-12-24T17:00"
   .lambda
      log.info:Executing foo-bar-task-3
```

The above **[due]** node is a date and time in the future for when you want your task to be scheduled
for executed. After the task has been executed, it will never execute again, unless you manually execute it.
**Notice** - You _cannot_ create a task with a due date being in the past.

### Repeating tasks

To have a task repeatedly being executed, you can choose between a whole range of repetition patterns. For instance,
to have a task scheduled for execution every Sunday at 10PM, you could create a task such as the following.

```
wait.tasks.create:task-id
   repeats:**.**.22.00.00.sunday
   .lambda
      log.info:Executing repeating task
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

To evaluate a task every Saturday and Sunday for instance, you can use `saturday|sunday` as your weekday.
Below is an example. Notice, weekdays are case insensitive.

```
wait.tasks.create:task-id
   repeats:**.**.22.00.00.saturday|SUNDAY
   .lambda
      log.info:Executing repeating task
```

#### Intervals

Evaluating your task every second/minute/hour can be done by using something such as the following.

```
wait.tasks.create:task-id
   repeats:50.seconds
   .lambda
      log.info:Executing repeating task
```

The above will evaluate your task every 50 second. The above _"seconds"_ can be exchanged with _"minutes"_, _"hours"_, _"days"_, _"weeks"_ or _"months"_. Notice, this allows you to have very large values, to have tasks that are repeating _very seldom_,
such as the following illustrates.

```
wait.tasks.create:task-id
   repeats:3650.days
   .lambda
      log.info:Executing seldomly repeating task once every 10 year
```

The above task will only be evaluated every 3650 days, which becomes once every 10 years.

#### Periodically scheduled tasks

To create a task that is executed on the first day of the month, at 5PM, you can use the following
repetition pattern.

```
wait.tasks.create:task-id
   repeats:**.01.05.00.00.**
   .lambda
      log.info:It is the first of the month, and the time is 5AM at night.
```

When supplying hours and minutes such as the above example illustrates, you must use military hours, implying
from 00:00 to 23:59.

#### Repeat format

The format of the **[repeats]** argument is as follows `MM.dd.HH.mm.ss.ww`. Where the entities implies the following.

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

The MM and dd arguments are optionally, and can be ommitted by using two asterix instead (\*\*) - At which
point it implies _"whatever"_. MM and dd _cannot_ be combined with weekdays, so if you supply MM and/or dd
arguments, you _cannot_ supply a weekday. If you supply a weekday pattern besides \*\*, you cannot supply
neither MM nor dd. These two patterns are mutually exclusive.

The double asterix `**` implies _"any value"_, and means _"undefined"_. The only mandatory arguments is HH mm and
ss, and must be supplied regardless of whether or not you create a weekday pattern, or a month/day pattern.

**Notice** - All times are interpreted as UTC times, and _not_ necessarily your local time. Have this in mind as you
create your tasks. The frontend however, will convert dates and times as you create tasks to your local timezone.

## Deleting a task

Use the **[wait.tasks.delete]** signal to delete a task. An example can be found below.

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

## License

Although most of Magic's source code is publicly available, Magic is _not_ Open Source or Free Software.
You have to obtain a valid license key to install it in production, and I normally charge a fee for such a
key. You can [obtain a license key here](https://servergardens.com/buy/).
Notice, 7 days after you put Magic into production, it will stop functioning, unless you have a valid
license for it.

* [Get licensed](https://servergardens.com/buy/)
