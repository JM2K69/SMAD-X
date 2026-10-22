# Labs

A lab puts the learner in a realistic situation (an organisation, a role, a triggering event and business constraints), then **grades the work automatically** with the audit, attack paths and a comparison with the starting domain.

## Opening a lab

| Menu | Content |
|---|---|
| **Labs > Built-in labs** | five general labs, bilingual |
| **Labs > Attack paths** | twelve labs, each in the interface language, with its own domain |
| **Labs > Open a lab...** | a `.smadx-lab.json` file received from a trainer |

The full list is in the [catalog](catalog.md).

Opening a lab replaces the current domain with the lab's starting domain. Its dates are shifted so that the lab takes place "today": an account that logged on yesterday still did, whatever day the lab is taken.

## The lab window

![A lab statement](../assets/screens/lab-statement.png)

- **On the left, the statement**: the context, your mission and your tools. It does not give the answer away.
- **On the right, the objectives**, with their points.
- **Hint** reveals a lead for the objective.
- **Check** grades every objective against the current state of the domain.
- **Start over** reloads the starting domain.
- **Export result** produces a Markdown report (statement, objectives, result, learner name) to hand in to the trainer.

## Grading

![A checked lab](../assets/screens/lab-checked.png)

Each objective is an automatic check. An objective that is not met explains what is missing. For an attack path, it shows the **remaining path**, step by step.

The screenshots show a demo lab built for this documentation and absent from the application. The solutions of the shipped labs are not published.

Labs also include **guard objectives**, already met at the start, which refuse brute-force solutions: deleting the service account, removing the service desk's legitimate delegation, emptying a business group…

## Writing your own labs

The [guide](create.md) explains how to create a lab: from the application, without programming, or with the repository's generators.
