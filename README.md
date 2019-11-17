
# Magic Lambda Scheduler

[![Build status](https://travis-ci.org/polterguy/magic.lambda.scheduler.svg?master)](https://travis-ci.org/polterguy/magic.lambda.scheduler)

Provides the ability to create scheduled tasks for [Magic](https://github.com/polterguy.magic). More specifically provides the following signals.

* [scheduler.tasks.create] - Creates a new scheduled task
* [scheduler.tasks.get] - Returns an existing scheduled task according to its name
* [scheduler.tasks.list] - Lists all scheduled tasks
* [scheduler.tasks.delete] - Deletes a named scheduled task
* [scheduler.start] - Starts the scheduler
* [scheduler.stop] - Stops the scheduler, implying all tasks will temporary be paused

When creating a task, you can create a task that only executes once. This is done as follows for instance.

```
scheduler.tasks.create:task-name
   when:date:"2019-12-24T17:00"
   .lambda
      /* Your tasks lambda object goes here /*
      .foo-something
```

The above **[when]** node is a date and time in the future for when you want your task to be scheduled
for evaluation. After the task has been evaluated, it will be removed from your scheduler, and never evaluate again.
The name of your task in the above example becomes _"task-name"_, and the task can be referenced later using this name.
The name must be unique, otherwise any previously created tasks with the same name will be overwritten.
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

You can choose any weekday you wish to have your task repeat. Below is an exhaustive list.

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

The above will evaluate your task every 50 second. The above second can be exchanged with _"minutes"_ or _"hours"_. Notice, you can
have _very large integer values_ here, to have tasks that are repeating _very seldom_, such as e.g. the following illustrates.

```
scheduler.tasks.create:task-name
   repeat:hours
      value:5000000
   .lambda
      /* Your tasks lambda object goes here /*
      .foo-something
```

The above task will only be evaluated every 5.000.000 hour, which of course becomes every 570 year, which is hopefully not a meaningful
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

**Notice** - All times are interpreted as UTC times, and _not_ necessarily your local time. Have this in mind as you create your tasks.
