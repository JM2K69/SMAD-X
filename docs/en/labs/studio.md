# SMAD-X Studio

**SMAD-X Studio** is the trainer's tool: an executable shipped in its own archive (see [Installation](../installation.md)), that creates labs, checks them and produces the trainer sheet. It does, with a window, what the [how-to](create.md) describes by hand. It also creates SMAR-X labs and grades their copies (see [SMAR-X labs](#smar-x-labs)).

Studio does not replace SMAD-X: the domain is prepared and fixed in SMAD-X, the lab is shaped in Studio, the learner does it in SMAD-X and hands in a **submission**, which Studio grades with the rest of the class.

![SMAD-X Studio home](../assets/screens/studio-home.png)

**View > Light theme** or **Dark theme** changes Studio's look; the choice is kept for the next session.

---

## Three ways to start

| **File** menu | Starting point | What Studio prepares |
|---|---|---|
| **New lab from a recommendation...** | a misconfiguration, among the audit rules and ANSSI control points | a healthy, populated domain with only that misconfiguration, the statement, the objectives and the trainer notes |
| **New lab from a domain (.smad-x.json)...** | a domain prepared and saved in SMAD-X | the statement template and the objectives suggested by the audit and the attack paths |
| **Convert a scenario into a lab...** | a `.smad-x.json` scenario whose description follows the *Context / The path / Fix it* outline | the statement taken from the context; the path and the fix go into the trainer notes |

**Open a lab (its folder)...** (`Ctrl+O`) reopens a lab by its folder, or by one of its role folders: Studio finds there everything the creation laid out (your copy, the subject, the fixed domain, the notes) and grades the submissions of `3-submissions` straight away. Dropping that folder on the Studio window does the same, and the start screen lists the **recent labs**, reopened in one click. **Open a lab file (.smadx-lab.json)...** resumes a lab saved on its own, with its notes.

**As soon as it is created**, Studio asks where to keep the lab, suggesting the folder of the previous one, and creates its folder there, laid out by role (see [The files](#the-files)): your copy, the subject to hand out and the trainer sheet are already written. Each step then writes in its place, without asking: the subject in `2-to-hand-out`, the sheet in `1-trainer`, the submissions you add in `3-submissions`, the grades and reports in `4-results`. If you cancel the folder choice, the lab stays open without being saved: **Save** will ask where to keep it.

### From a recommendation

![Choosing the recommendation](../assets/screens/studio-recipe.png)

Each audit rule has its recipe, with the matching ANSSI control point and its level (N1 to N4). The list can be filtered by rule id, ANSSI point or title word. Choose the lab language, the Windows Server version that promoted the domain (2019, 2022 or 2025) and the domain name: Studio builds a realistic directory (departments, users, workstations), checks that it is healthy, then injects **only** the chosen misconfiguration. This starting domain comes from a new AD that Studio hardens: built-in accounts fixed, a 12-character password policy with lockout, a join quota of 0, delegation groups for the service desk and IT. Recipes TIER-05 to TIER-08 (compliance with a tiering model) start from this domain migrated to Microsoft's model, free of findings once migrated (see [Tiering models](../tiering.md)): they only combine with each other, and their fix moves objects (**Cut**, **Paste**) or restores a GPO setting.

#### Combining several recommendations

Tick several rows to put their misconfigurations **in the same domain**: the lab gets one objective per targeted rule, the objectives suggested by the audit and the attack paths, and the guards of every recipe. Level and duration grow with the number of misconfigurations, and the trainer notes have one section per rule. With nothing ticked, the highlighted row is used; **Untick all** goes back to a single recommendation.

Some recipes do not go together: two recipes that create the same object (KRB-01 and KRB-02 both create the business application's service account), or a recipe that cancels another one's misconfiguration. Studio explains it under the list and disables **Create the lab** while the combination is not possible.

The statement it writes stays general ("check compliance before the yearly audit"): it names neither the rule nor the object to fix. Enrich it with your organisation's context.

#### The fixed domain

Studio also produces the **fixed domain**: the starting domain with the expected fix of each chosen recommendation applied, the one the rule sheet describes, without deleting anything. Validation uses it to check that it gets all the objectives' points, and the trainer sheet lists what it changes. It is saved with the lab, in `1-trainer/fixed-domain.smad-x.json` (see [The files](#the-files)): open it in SMAD-X to show the fix, but do not give it to learners.

---

## The tabs

### Lab

![Lab tab](../assets/screens/studio-lab.png)

Title, identifier (the file name), language, menu series (general labs or attack paths), level and duration. The starting domain summary gives the Windows Server version that promoted it, the number of objects, the attack paths outside Tier 0 and the audit score.

A lab is either **bilingual** or exists in **one language** only. The *Language of the texts being edited* list, at the top of the window, chooses which texts you edit.

**Exam mode**: in SMAD-X, the lab has no hints, no score and no "Start over", and no other domain can be opened; the time spent is measured. The learner saves a submission and hands it in. Two settings complete exam mode:

- **Allow "Check"**: the learner sees which objectives are met, with no score and no explanation;
- **Tools closed during the exam**: security audit, the "How to fix" section of the sheets, attack paths, before / after comparison. All are open by default: they help analyse, but they also show what grading checks.

**Your copy and the subject**: the file Studio saves in `1-trainer` is **your copy**, with the hints and the answer key of the questions; submissions are graded with it. It is not handed out: for an exam, SMAD-X refuses to open it. At every save, Studio also writes the **subject**, under the same name, in `2-to-hand-out`: without the answer key and, for an exam, without the hints. That is the file to hand out. **Export the subject for learners...** (**File** menu, or the button in the Lab tab) saves the lab and rewrites the subject in `2-to-hand-out`, without asking: the subject and the copy that will grade the submissions stay identical. For a lab saved on its own by an earlier version, it asks where to write the subject, and refuses to write it over your copy.

Closing Studio with an unsaved lab or notes asks you to save.

### Statement

![Statement tab](../assets/screens/studio-statement.png)

The statement is written in Markdown, with a preview as the learner will see it. **Insert the template** lays out the *Context / Your mission / Your tools* outline.

!!! warning "The statement never gives the answer"
    It describes the situation, the mission and the constraints. The attack path and the fix go into the **trainer notes**. Validation rejects a statement that contains a solution section.

### Objectives

![Objectives tab](../assets/screens/studio-objectives.png)

On the left, the objectives and the total points; on the right, the selected objective: title, hint, points and check. Only the fields the chosen check needs are shown, and object names autocomplete from the starting domain. The available checks are described in the [how-to](create.md).

**Suggest objectives** analyses the starting domain and suggests what the lab does not contain yet:

![Objective suggestions](../assets/screens/studio-suggestions.png)

- one objective per attack path outside Tier 0, and "no path left to domain control";
- one objective per failed audit rule;
- **guards** (**Guard** box, 🛡 in the list), already met at start, that refuse a brute-force solution (deleting the account instead of fixing it). A guard earns no points; broken, it cancels the objectives that concerned the protected object at start: its audit finding, the attack paths that went through it, the checks that name it.

Recommended suggestions are ticked; **Add selection** adds them to the list.

**Partial credit** (◐ in the list): for a check that can be counted, the objective earns the share of the way covered since the start. It applies to a rule's findings over the whole domain (3 accounts fixed out of 4: 75 % of the points), to the findings of a severity, to the audit score (the share of the way to the target score) and to the attack paths outside Tier 0 or from any domain user. A broken guard also cancels the share earned.

### Questions

![Questions tab](../assets/screens/studio-questions.png)

Comprehension questions, asked in the lab window with the objectives and graded with them. **Add a question**, then choose its form:

| Form | The learner… | Answer key | Partial credit |
|---|---|---|---|
| **Single choice** | ticks one answer | the right answer | no |
| **Multiple choice** | ticks several answers | the right answers | each right answer ticked counts, each wrong one counts against |
| **Yes / no** | answers yes or no to a statement | the right answer | no |
| **Matching** | matches each item with a proposal, from a list | the pairs; distractors are possible | the share of items matched right |
| **Written answer** | writes the answer | the accepted answers, one per line (optional) | no |

A written answer is recognised regardless of case, accents, extra spaces and final punctuation. Any other answer, or a question with no accepted answer, is graded **by hand** in the Submissions tab.

The answer key stays in your copy. The exported subject holds neither the right choices, nor the pairs, nor the accepted answers, and a matching question's proposals are shuffled in it. In an exam, SMAD-X saves the answers in the submission without grading them; outside exams, the lab handed out holds the answer key, and **Check** grades the questions too (validation points it out).

### Validation

![Validation tab](../assets/screens/studio-validation.png)

**Validate the lab** runs the checks of the shipped labs' tests:

- texts present in every language of the lab, unique ids, positive points;
- no main objective met at start, every guard met, each guard actually effective, and the shortcut it refuses earns no more than half the points;
- cited objects and rules exist in the starting domain;
- no solution visible to the learner, neither in the statement nor in the domain descriptions;
- complete questions: texts, at least two choices, a right answer (exactly one for single choice or yes / no), each matching item linked to a proposal.

#### The solution

Validation also checks the solution, the **fixed domain**: it must get all the objectives' points. There are three ways to get it:

| How | What Studio does |
|---|---|
| the lab comes from recommendations | it produces the fixed domain with the lab: the expected fix of each recommendation |
| **Suggest a solution** | for any lab: it fixes each finding of the starting audit as its rule sheet describes, deleting nothing (it disables, removes a membership or a delegation, renews a password), the most severe first; then it fulfils the objectives the audit does not see: a delegation to give, a membership, an account option, a tier. A fix that would break a guard is set aside. |
| **Choose the fixed domain (solution)...** | your own fix, made in SMAD-X as a learner would and saved as `.smad-x.json`: Studio brings it back to the dates of the start |

In all three cases, the fixed domain is kept with the lab, in `1-trainer`, and the trainer sheet lists what it changes. What a suggested solution does not reach shows in the validation ("The solution fails"). To finish it: save, open `1-trainer/fixed-domain.smad-x.json` in SMAD-X, do the rest, save, then **Choose the fixed domain (solution)...**.

### Trainer sheet

The **trainer notes** (the solution, things to watch for) and the trainer sheet preview: objectives, guards, questions and their answer key, attack paths and audit of the starting domain, what the fixed domain changes, then the notes. **Export trainer sheet...** saves the lab and writes the sheet as Markdown in `1-trainer`.

---

## Grading a class

![Submissions tab](../assets/screens/studio-submissions.png)

Each learner saves their work in SMAD-X with **Save my submission**: a `.smadx-submission.json` file holding the lab they received, their domain, their answers to the questions and the hints viewed. The simplest: with the lab open, **drop the submissions on the Studio window**, or a folder of submissions (a USB stick, an assignment downloaded from the school platform): Studio files them in `3-submissions`, with the grades by hand that come with them, and grades them straight away. A submission already filed is not copied again; another file of the same name gets a number. You can also put the submissions into `3-submissions` yourself, from the file explorer: they are graded when the lab opens, or as soon as you come back to Studio if it is already open. Opened by its folder, the lab is always **your copy** (`1-trainer`), not the subject, which has no answer key. In the **Submissions** tab, the same submissions can also be added with buttons:

- **Add submissions...**: pick one or several submissions at once;
- **Add a folder...**: every submission in the folder and its subfolders, filed with their subfolders (a Moodle download has one folder per learner, and the submissions may have the same name there).

Studio grades each submission with **your** copy of the lab, as it is open: the starting domain is prepared again at the date the learner started, and the check runs at the date of the submission. The score the learner may have seen is never reused. After changing objectives or points, **Regrade** grades everything again.

Each row gives the grade (to the hundredth with partial credit; followed by `*` when answers remain to be graded), the percentage, the number of hints viewed, the time spent, one dot per objective then per question (green met, orange in part, red not met, grey to grade; hover for details) and the remarks:

| Remark | Meaning |
|---|---|
| submission for another lab | it is not graded |
| submission changed outside SMAD-X | its seal no longer matches its content: name, dates, hints or domain edited by hand |
| same work session as …'s submission | one submission handed in under several names: one is a copy of the other |
| the domain does not come from this submission's session | someone else's work, handed in under one's own submission; Studio names the original learner if it has their submission |
| unsealed submission, no work session | the submission was not saved by SMAD-X: these checks are impossible |
| starting domain replaced, session check impossible | the lab received, inside the submission, was replaced by another domain to escape the session check |
| exam submission made outside exam mode, exam settings changed | the lab received was changed to get checking, "Start over" or closed tools back |
| hints viewed during the exam | the submission carries hints although the exam has none |
| the learner received your copy instead of the subject | the lab received held the hints or the answer key |
| question with no answer key: open your copy | the lab open in Studio is the subject: the questions cannot be graded |
| the answer changed since you graded it | the learner handed in a new submission: grade the answer again |
| time exceeded | the learner spent more than the planned duration (nothing was blocked) |
| submission made with another SMAD-X version | its audit rules differ from those of your Studio: the grade may differ from the learner's; grade a class with a single version |
| other starting domain | the submission was made on another version of the lab: the grade may be wrong |
| no name, several submissions | check before recording the grade |
| unreadable submission | the file is damaged or is not a SMAD-X submission |

Red remarks need your attention; the grade stays displayed. Two identical solutions, each done on its own, are not reported: what gives a copy away is not the solution, it is the work session. SMAD-X draws it at random when the lab opens, derives from it the internal identifiers of every object in the learner's domain, and seals each submission with a SHA-256 fingerprint of all its content.

!!! note "What these checks guarantee"
    They catch the common cases: a submission edited in a text editor, a classmate's submission handed in under one's own name, a domain taken from someone else. The format is public and SMAD-X runs on the learner's computer: they do not stop a determined forger who would rewrite the whole file consistently. An absolute guarantee would require collecting submissions on a machine the learner does not control.

### Grading by hand and commenting

Select a submission: the **Selected submission** panel shows each answer, the automatic grading and a **Points given** field. Left empty, the automatic grading counts; filled in, it replaces it, for instance for a written answer Studio did not recognise. The **comment** appears in the learner's report. **Save the grade** writes it all beside the submission, in `<submission>.smadx-marks.json`: the sealed submission itself is never changed. If the learner hands in a new submission with another answer, the grade given by hand no longer applies.

### Exporting the grades

The **Success per objective** tab shows what the class understood and what to go over again, objective by objective and question by question. The exports use the **Grade out of** scale (20 by default) and are written, without asking anything, in the lab folder's `4-results` folder:

| Button | Produces |
|---|---|
| **Export grades (CSV)...** | a spreadsheet for Excel: name, ID, points, grade out of the scale, hints, time, answers to grade, points of each objective and each question, remarks. The grade column can be pasted into the school platform. |
| **Export for Moodle (CSV)...** | the Moodle grade import file (*Grades > Import > CSV file*): the learner's ID, name and grade out of the scale. In Moodle, map the ID column to the students' email address or ID number, and the grade to the grade item, whose maximum grade must be the scale. |
| **Individual reports (PDF)...** | one PDF per graded submission, in `4-results/reports`: the grade, each objective with its explanation, the answers to the questions and the comment. It holds neither the answer key nor the trainer-only remarks. |
| **Export class results (Markdown)...** | the class results: average, median, everyone's grade, success per objective and per question. |

In SMAD-X, the learner can enter an **ID** (their number or address) beside their name: the exports use it.

---

## SMAR-X labs

Studio also creates [SMAR-X labs](../smar-x/labs.md): an access need to meet with only the permissions required, on Linux and Windows servers. One lab is open at a time, from SMAD-X or from SMAR-X; the **SMAR-X lab** badge shows it.

| **File** menu | What it does |
|---|---|
| **New SMAR-X lab from a recipe...** | a lab generated for a company, its departments and its server (see [From a recipe](#from-a-recipe)) |
| **New SMAR-X lab on the sample servers** | a lab on SMAR-X's sample servers, Linux and Windows |
| **New SMAR-X lab from servers (.smar-x.json)...** | a lab on servers prepared in SMAR-X and saved with **Save…** |
| **Open a SMAR-X lab (.smarx-lab.json)...** | reopens a lab; its solution, stored next to it, is read again |

**Save** (`Ctrl+S`), **Save as** and **Export the subject** apply to the open lab. The subject has no answer key for the questions and, for an exam, no hints.

### From a recipe

**New SMAR-X lab from a recipe...** generates a full lab for a company: the statement, the objectives and guards, the starting servers, the fixed servers (the solution) and the trainer notes. Studio validates it at once, then asks where to lay it out.

![SMAR-X recipes in Studio](../assets/screens/studio-smarx-recipe.png)

| Setting | What it gives |
|---|---|
| **Company** | the domain name: "Martin Works" gives `martinworks.local` (`MARTINWORKS`) |
| **Departments** | 2 to 8 departments, separated by commas. Each one gets its folder (`D:\Partages\<department>`, `/srv/<department>`), its global group `G_<department>`, its domain local groups `DL_<department>_L` and `DL_<department>_LM`, and two accounts |
| **Server** | Windows, Linux, or both with the same need |
| **Lab language** | the language chosen when opening; the statement is written in both |

The **business rules** are the same for every recipe, and the statement gives them in a table: each department reads and modifies its folder; a Direction (a department whose name starts with "Direction"), if there is one, reads and modifies every folder; the second department reads the first one's folder. Group and folder names lose their accents and spaces.

| Recipe | Server | The scenario |
|---|---|---|
| `SMX-ARBO` | Windows, Linux or both | to build: open each department's folder following the business rules |
| `SMX-GROUPE` | Windows, Linux or both | an account is in another department's group |
| `SMX-NOMINATIF` | Windows | ACLs name accounts and global groups, to move to AGDLP |
| `SMX-OUVERT` | Windows, Linux or both | a department's folder is writable by everyone |
| `SMX-PARTAGE` | Windows | NTFS grants, but the share blocks |
| `SMX-AUDIT` | Linux | a `777` folder, a setuid, a drop folder without sticky bit, an ACL mask |

Each generated lab is checked by SMAR-X's tests, for each recipe, each server and several companies: it fails at the start, its guards hold, and its solution meets every objective. The known easy ways out are caught by the guards.

The lab is laid out in a folder by role, like a SMAD-X lab (see [The files](#the-files)):

```
smx-arbo-martinworks-mixte/
├── README.md
├── 1-trainer/         your copy, trainer-notes.md, fixed-servers.smar-x.json
├── 2-to-hand-out/     the subject, to open in SMAR-X
├── 3-submissions/     the copies (.smarx-submission.json)
└── 4-results/         grades.csv, grades-moodle.csv, class-results.md
```

The trainer notes give the commands that take the starting servers to the fixed ones. Hand out only `2-to-hand-out`.

### The tabs

The **Lab**, **Statement** and **Questions** tabs are those of a SMAD-X lab. The **Objectives** tab offers SMAR-X's checks:

![Objectives of a SMAR-X lab](../assets/screens/studio-smarx-objectives.png)

*Screenshot: a demo lab, specific to the documentation.*

| Check | Fields |
|---|---|
| Access allowed, access denied | account, operation, path, and the share the access goes through (or locally) |
| Exactly the accesses needed | the accounts, paths and operations checked, one per line; the accesses needed, one per line (`account operation path`); the share |
| What a new file gets | the account that creates, the folder, the other account and the operation it must be able to perform |
| Hygiene rule | the rule, and the path or the share concerned |
| The account does not change | the account, or `*` for all |
| The path's permissions do not change | the path |

The lists offer the starting servers' accounts, paths and shares.

- **"Exactly the accesses needed"**: each cell (account, path, operation) is right when the access is allowed exactly when it is needed. Without **partial credit**, the objective is met when every cell is right. With it, each right cell earns its share of the points, including the denials already right at the start.
- **A guard** is met at the start and earns nothing. Broken, it cancels the objectives about the same account or path. An unchanged account and a path's unchanged permissions are guards.

### Validating

**Validate the lab** checks:

- that it has an ID, a title in both languages and at least one objective;
- that at the start, no scored objective is met and every guard holds;
- that the **solution** meets every objective.

The solution is the fixed servers, prepared in SMAR-X then saved (`.smar-x.json`): **Choose the fixed servers (solution)...** loads them. Studio stores it next to the lab, as `id.solution.smar-x.json`, never inside it.

![Validation of a SMAR-X lab](../assets/screens/studio-smarx-validation.png)

### Grading the copies

**Add submissions...** loads the copies (`.smarx-submission.json`) handed in by the learners. Studio grades them with the lab as it is open, answer key included. It flags:

- a copy edited after it was saved (broken seal), or without a seal;
- the copy of another lab;
- two copies of the same work session: one is a copy of the other;
- the written answers to grade by hand.

Each copy has its grade, its dots (one per objective, then per question: green met, orange partly, red not met; hovering gives the explanation) and its remarks.

![Copies of a SMAR-X lab](../assets/screens/studio-smarx-submissions.png)

The exports are those of a SMAD-X lab (see [Exporting the grades](#exporting-the-grades)), on the **Grade out of** scale (20 by default). For a lab laid out by role, they are written without asking into `4-results`, and the copies of `3-submissions` are graded as soon as the lab opens; otherwise, Studio asks where to save them:

| Button | Produces |
|---|---|
| **Export grades (CSV)...** | one line per copy, with the same columns as a SMAD-X lab |
| **Export for Moodle (CSV)...** | the Moodle grade import file: ID, name, grade out of the scale |
| **Export class results (Markdown)...** | average, median, everyone's grade, success per objective and per question |

There is no PDF report per learner for a SMAR-X lab. In SMAR-X too, the learner can enter an **ID** beside their name: the exports use it. The [command line](#command-line) also grades the copies of a SMAR-X lab.

---

## The files

Studio lays each new lab out in a folder named after it, created in the chosen folder (or in the chosen folder itself, if it is empty; a lab of the same name is never overwritten: the new folder gets a number), with one subfolder per role. **File > Save to a folder...** moves it elsewhere. Folders and files have English or French names, depending on the interface language when the lab folder is created:

```
iam-review/
├── README.md                the role of each folder
├── 1-trainer/               keep it to yourself
│   ├── iam-review.smadx-lab.json   your copy: statement, starting domain, objectives, questions, hints and answer key
│   ├── trainer-notes.md            the trainer notes (the solution)
│   ├── fixed-domain.smad-x.json    the fixed domain, for a lab created from recommendations
│   └── trainer-sheet.md            the trainer sheet
├── 2-to-hand-out/
│   └── iam-review.smadx-lab.json   the subject: the only file to give learners
├── 3-submissions/           the submissions handed in (<id>_<name>.smadx-submission.json)
│                            and, beside each one, your grades by hand (<id>_<name>.smadx-marks.json)
└── 4-results/               grades.csv, grades-moodle.csv, class-results.md, reports/ (one PDF per learner)
```

At every **Save**, Studio rewrites your copy, the notes, the fixed domain, the sheet, the subject and the README. Your copy and the subject have the same name: the folder tells which one to hand out. The notes and the fixed domain are never written into the lab, and the subject has no answer key: handing out `2-to-hand-out` does not reveal the solution.

Change the lab ID (**Lab** tab): at the next save, your copy and the subject take the new name, and so does the folder if it had the old one. Do it before handing out the subject: a submission carries the ID of the lab it was made from, and Studio refuses to grade it with another one. **File > Open the lab folder** (or the button beside its path) opens it in the file explorer.

A lab saved on its own by an earlier version still opens, with its notes beside it (`<id>.smadx-lab.notes.md`); **Save to a folder...** lays it out by role.

---

## Command line

Studio also produces labs in batches and grades a class, without a window:

```bash
SMAD-X.Studio list-recipes --lang en
```

```bash
SMAD-X.Studio recipe KRB-01 DLG-02 --lang en --domain training.lab --out labs
```

```bash
SMAD-X.Studio recipe KRB-01 DLG-02 ACL-01 --combine --lang en --out labs
```

```bash
SMAD-X.Studio recipe all --lang fr --out exercices
```

```bash
SMAD-X.Studio convert scenarios --lang en --out labs
```

```bash
SMAD-X.Studio validate labs --solution fixed.smad-x.json
```

```bash
SMAD-X.Studio grade labs/anssi-krb-01 --lang en
```

```bash
SMAD-X.Studio grade labs/anssi-krb-01/1-trainer/anssi-krb-01.smadx-lab.json submissions-group-a submissions-group-b --lang en --csv grades.csv --moodle moodle.csv --reports reports --report results.md
```

```bash
SMAD-X.Studio grade permissions/hr-share.smarx-lab.json hr-copies --lang en --csv grades.csv --moodle moodle.csv --report results.md
```

```bash
SMAD-X.Studio recipe SMX-ARBO SMX-GROUPE --company "Martin Works" --services "Accounting,Production,Sales,Direction" --platform both --lang en --out labs
```

| Command | Purpose |
|---|---|
| `list-recipes` | lists the recipes: rule, severity, ANSSI point, title |
| `recipe <rule>...` or `recipe all` | creates one lab per recipe, each in its own folder laid out by role (your copy, notes, fixed domain, sheet, subject); with `--combine`, a single lab that brings them together |
| `convert <file or folder>...` | converts `.smad-x.json` scenarios, each into its own folder |
| `validate <file or folder>...` | validates labs (every one in a folder, subfolders included, subjects excepted), with their fixed domain, or the one given by `--solution` |
| `export-subject <file or folder>...` | rewrites the subject of each lab into its `2-to-hand-out` folder, or into `--out` |
| `grade <lab folder>` | grades the submissions of `3-submissions` and writes every result to `4-results` |
| `grade <lab> <submission or folder>...` | grades the given submissions, wherever they are; `--csv`, `--moodle`, `--reports` and `--report` choose the files. `+` met, `~` in part, `-` not met, `?` to grade by hand in Studio |
| `recipe <SMX-ID>...` or `recipe smx-all` | SMAR-X recipes (see [From a recipe](#from-a-recipe)): one lab per recipe, for the company of `--company`, `--services` and `--platform`, each in its folder laid out by role (your copy, notes, fixed servers, subject) |
| `grade <SMAR-X lab folder>` | grades the copies of `3-submissions` and writes every result to `4-results` |
| `grade <SMAR-X lab> [<copy or folder>...]` | grades the copies of a SMAR-X lab: the `.smarx-lab.json` file, or a folder holding just one. Without copies given, it takes every `.smarx-submission.json` of its folder, subfolders included. Same lines, same signs and same options, except `--reports`, which is for SMAD-X labs only |

| Option | Effect |
|---|---|
| `--lang fr` or `--lang en` | language of the lab and of the messages |
| `--domain <name>` | domain name created by a recipe (`corp.example.lab` by default) |
| `--server 2019`, `2022` or `2025` | Windows Server version that promoted this domain (`2022` by default) |
| `--out <folder>` | folder in which the lab folders are created (the current folder by default) |
| `--date yyyy-mm-dd` | reference date: the same date gives the same file, byte for byte |
| `--solution <file>` | fixed domain to grade during validation |
| `--combine` | `recipe`: a single lab with every misconfiguration in the same domain |
| `--exam` | `recipe`, `convert`: lab in exam mode |
| `--exam-check` | with `--exam`: "Check" allowed (objectives met, no score) |
| `--exam-close audit,fixes,paths,comparison` | with `--exam`: tools closed during the exam |
| `--csv <file>` | `grade`: grade spreadsheet for Excel |
| `--moodle <file>` | `grade`: Moodle grade import file |
| `--reports <folder>` | `grade`: one PDF report per learner (SMAD-X labs only) |
| `--scale <n>` | `grade`: scale of the exported grades (20 by default) |
| `--report <file>` | `grade`: class results in Markdown |
| `--company <name>` | SMAR-X `recipe`: the company, which gives the domain (`Lumen` by default) |
| `--services <A,B,C>` | SMAR-X `recipe`: 2 to 8 departments (`Compta,RH,Ventes,Direction` by default) |
| `--platform windows`, `linux` or `both` | SMAR-X `recipe`: the server (`windows` by default); a recipe that does not run on it is refused |
| `--verbose` | also shows the checks that passed |

Each lab written is validated straight away. The exit code is **0** when all is well, **1** when a lab has errors (it is still written, to be fixed in Studio), the recipes cannot be combined, there is no submission to grade or a submission needs attention (not graded, changed or copied), and **2** for a wrong command, `--reports` with a SMAR-X lab included.
