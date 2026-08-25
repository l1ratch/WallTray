# AGENTS.md — Multi-Agent Project Operating Rules

These rules apply to every model/agent working in this repository.

The active model may change between planning, implementation, debugging, review, or even inside the same chat. Never assume another agent has access to your private reasoning or reliable memory of earlier turns.

The repository plus `.agent/` files are the shared inter-agent memory.

---

## 1. Priority

Follow, in order:

1. Current explicit user instructions.
2. This `AGENTS.md`.
3. Durable project decisions in `.agent/DECISIONS.md`.
4. Verified current state in `.agent/STATE.md`.
5. Active plan in `.agent/PLAN.md`.
6. Your preferred implementation approach.

When uncertain, preserve existing work and choose the reversible path.

---

## 2. Core invariants

### Current disk state is authoritative

Current files on disk are the source of truth.

Do not treat any of these as newer than the worktree:

- model memory;
- chat summaries;
- old tool output;
- an earlier file read;
- Git `HEAD`;
- an old commit;
- cached file contents;
- a previous agent's description.

Before editing an existing file, read its current relevant contents.

### Unknown work is valuable

Treat all existing uncommitted, untracked, or unexplained changes as valuable.

If you do not remember a feature/change, that does NOT mean it is obsolete or safe to remove.

### Preserve unrelated behavior

A task is not permission to rewrite unrelated code, UI, behavior, configuration, tests, or assets.

Prefer the smallest correct change.

Unexpected removal or broad rewriting of unrelated functionality is a regression warning.

### Fix forward

If your own edit causes a failure, repair the current worktree.

Do not restore whole files or repository state from an older Git version just because your latest edit was wrong.

Reverse only exact hunks you can positively identify as belonging to your failed change.

### Externalize important state

Anything another agent needs must be recorded in the repository or `.agent/` files.

Do not rely on private reasoning or conversation memory for project continuity.

Record conclusions and evidence, not chain-of-thought.

---

## 3. Required coordination files

Keep these files:

```text
.agent/
├── STATE.md
├── PLAN.md
├── HANDOFF.md
├── DECISIONS.md
└── ISSUES.md
```

If missing, create them from the repository templates.

Purpose:

- `STATE.md` — facts that are true NOW.
- `PLAN.md` — current goal, constraints, acceptance criteria, active steps.
- `HANDOFF.md` — latest agent/role transfer.
- `DECISIONS.md` — durable architecture/product/security decisions.
- `ISSUES.md` — actionable unresolved problems/findings and useful regression history.

Keep them concise. They are not chat logs.

---

## 4. Startup / takeover protocol

At the start of meaningful work, or whenever taking over from another model/role:

1. Read `AGENTS.md`.
2. If Ponytail is available in the current host, keep it active and follow the Ponytail integration rules in this file.
3. Read `.agent/STATE.md`.
4. Read `.agent/HANDOFF.md`.
5. Read `.agent/PLAN.md` if active.
6. Read relevant decisions/issues.
7. Inspect `git status`.
8. Inspect relevant existing diff before editing already-modified files.
9. Read current versions of files you intend to edit.

Do not re-investigate the whole repository if verified coordination data already answers the question.

If coordination files conflict with the current worktree or test output, the worktree/evidence wins; correct the stale coordination files.

---

## 5. Source-of-truth order

When information conflicts, trust in this order:

1. Current files on disk.
2. Current `git status` / `git diff`.
3. Reproducible build/test/runtime output from this worktree.
4. `.agent/STATE.md`.
5. `.agent/DECISIONS.md`.
6. `.agent/PLAN.md` / `.agent/ISSUES.md`.
7. `.agent/HANDOFF.md`.
8. Visible conversation.
9. Model memory/summaries.

Never overwrite a higher-authority state using a lower-authority source without verifying the discrepancy.

---

## 6. Multi-model / multi-role workflow

Treat every phase boundary as a real handoff, even if another model is selected in the SAME chat.

A phase boundary includes:

- plan completed;
- implementation milestone completed;
- bug reproduced;
- fix completed;
- review/audit completed;
- architecture or requirement changed;
- work blocked;
- context/session is becoming long;
- control is returning to the user.

Before ending a meaningful phase:

1. update `PLAN.md`;
2. update `STATE.md`;
3. update `ISSUES.md`;
4. record durable decisions;
5. rewrite `HANDOFF.md`.

The next agent must be able to continue without your private reasoning.

---

## 7. Role rules

### Planner

A planner must:

- inspect current repository state first;
- separate verified facts from assumptions;
- define goal and non-goals;
- define acceptance criteria;
- record compatibility/safety invariants;
- identify likely affected components;
- write/update `.agent/PLAN.md`;
- record durable decisions;
- leave implementation-ready handoff.

Do not force the repository to match a stale plan. Update the plan when reality differs.

### Implementer

An implementer must:

- read the plan/handoff;
- validate them against the current worktree;
- preserve unrelated work;
- implement in coherent steps;
- verify meaningful milestones;
- update plan statuses;
- update current state;
- record deviations and unresolved issues;
- leave a precise handoff.

### Fixer / debugger

A fixer must:

- establish the failure from evidence;
- inspect current files and current diff;
- reproduce narrowly where practical;
- preserve previously working features;
- fix forward;
- distinguish confirmed root cause from hypothesis;
- record tests/commands and exact outcomes;
- never repair a mistake by restoring an entire old file.

### Reviewer / auditor

A reviewer must:

- review the current worktree;
- distinguish confirmed findings from hypotheses;
- provide evidence and affected locations;
- avoid implementation changes unless explicitly assigned;
- place actionable findings in `.agent/ISSUES.md`;
- leave a review handoff.

---

## 8. Handoff quality

`.agent/HANDOFF.md` must state, concisely:

- current task;
- what was actually completed;
- what is currently true;
- relevant/changed files;
- verification performed and results;
- important existing work that must be preserved;
- open problems/uncertainty;
- durable decisions made;
- exact recommended next actions;
- specific actions the next agent must NOT take.

Do not write vague entries such as "worked on UI" or "fixed stuff".

Use file paths, symbols, commands, results, commit hashes, and concrete behavior.

---

## 9. State quality

`.agent/STATE.md` must contain current facts, not history:

- current objective;
- branch / HEAD;
- last known-good checkpoint;
- whether worktree is dirty;
- important uncommitted areas;
- verified working features/invariants;
- current implementation facts;
- build/test/manual-check status;
- active risks/constraints;
- next safe action.

Rewrite stale facts instead of appending a diary.

---

## 10. Planning quality

`.agent/PLAN.md` uses:

- `[ ]` not started
- `[-]` in progress
- `[x]` completed AND verified
- `[!]` blocked/invalidated

A plan must include:

- Goal
- Non-goals
- Acceptance criteria
- Safety/compatibility invariants
- Ordered steps
- Dependencies/assumptions
- Deviations discovered during implementation

Never mark a step complete merely because code was written.

---

## 11. Decisions and issues

### Durable decisions

Use `.agent/DECISIONS.md` only for choices future agents must respect.

Do not silently delete old decisions. Mark superseded decisions and add the replacement.

### Issues

Use `.agent/ISSUES.md` for actionable problems/findings.

Separate:

- CONFIRMED facts;
- UNVERIFIED hypotheses;
- reproduction evidence;
- attempts;
- resolution;
- verification.

Never present a guess as a confirmed root cause.

---

## 12. Git / worktree safety

### Never use destructive recovery for your own mistakes

Do not use these as a repair strategy:

```text
git checkout -- <file>
git restore <file>
git restore .
git reset --hard
git clean
```

Do not overwrite current files with content copied from an old commit.

Do not delete untracked files as "cleanup".

Do not force-push or rewrite published history unless the user explicitly requested that exact operation.

### Dirty worktree

Do not switch branches, reset, restore, or perform broad repository operations if they may hide, overwrite, or mix valuable uncommitted work.

### Checkpoints

Unless the user explicitly forbids commits, local checkpoint commits are allowed and encouraged after a substantial logical milestone that is:

- coherent;
- working;
- verified.

Example:

```text
checkpoint: completed tray window redesign
```

Never push checkpoint commits automatically.

Do not checkpoint every tiny edit.

Do not stage secrets, credentials, `.env`, ignored sensitive files, generated build output, or unrelated files just to make a checkpoint.

If a safe checkpoint cannot be created, preserve the dirty worktree and clearly record it in `STATE.md` and `HANDOFF.md`.

---

## 13. Editing discipline

### Read before edit

Read the current relevant section before changing an existing file.

Refresh your read if the file may have changed since you last inspected it.

### Prefer targeted patches

Prefer targeted edits over reconstructing whole existing files.

A whole-file rewrite requires a real technical reason and verification that unrelated content is preserved.

### Never recreate current files from memory

Do not rebuild a source file from:

- remembered code;
- old assistant output;
- an earlier tool result;
- Git history;
- an old plan.

Always use the current disk file as the base.

### Inspect diffs

After substantial edits, inspect the relevant diff.

Check specifically for:

- unrelated deletions;
- accidental feature removal;
- large unexpected formatting churn;
- encoding/line-ending churn;
- scope expansion.

A surprisingly large diff is a warning condition.

### Encoding

Do not "fix" an encoding problem by restoring an older logical version of the file.

Preserve current content and repair encoding separately.

---

## 14. Verification

Never claim completion solely because code was edited.

Prefer:

1. targeted tests;
2. build/typecheck/lint;
3. focused runtime/manual checks;
4. diff inspection.

Use these meanings exactly:

- `PASS` — run and succeeded.
- `FAIL` — run and failed.
- `NOT RUN` — not executed.
- `UNVERIFIED` — believed but not proven.

If verification is blocked, record the blocker instead of implying success.

---

## 15. Interaction behavior

### Be autonomous on reversible work

Do not ask for routine confirmations.

Make normal reversible decisions independently when they fit the task, current plan, and durable project decisions.

### Destructive uncertainty

If an action is destructive/irreversible and there is no safe non-destructive alternative:

- do not perform it;
- preserve current work;
- record the blocker;
- continue with any safe work that remains possible.

### Do not hide failures

Report failures factually.

Do not silently substitute behavior that changes requirements just to make tests/build pass.

### No silent scope expansion

Do not add unrelated:

- refactors;
- redesigns;
- dependency swaps;
- architecture changes;
- cleanup;
- feature removals.

If extra work becomes necessary for correctness, record why.

### Persist corrected requirements

When the user corrects a lasting requirement, behavior, design direction, compatibility constraint, or security rule, write it to `STATE.md` or `DECISIONS.md`.

Do not leave important corrections only in chat history.

---

## 16. External Research and MCP Search

External research tools are shared capabilities for every model/agent working in this repository.

The active model may have reliable native web search, may have no native search, or may change during the same task. Project workflow must not depend on a single model-specific search feature.

When available, use the shared MCP research layer:

- `exa` — general/current web search and webpage retrieval;
- `context7` — current library, framework, SDK, and API documentation;
- `gh_grep` — real-world public code examples and usage patterns.

These tools complement native model search. They do not replace repository inspection.

### Search priority

Choose the narrowest authoritative source that can answer the question.

1. If the repository/current worktree already contains the authoritative answer, use it first.
2. For library/framework/SDK/API documentation, prefer `context7`.
3. For real-world implementation examples or ambiguous API usage, prefer `gh_grep`.
4. For general/current web research, use reliable native web search when available.
5. Use `exa` when:
   - native search is unavailable;
   - native search fails or returns weak/insufficient results;
   - independent verification is useful;
   - full content of an external webpage needs to be retrieved;
   - another model taking over must have the same external-search capability.
6. For important technical claims, prefer primary/official sources over blogs, summaries, reposts, or random examples.

### Do not mechanically call every search tool

Use the minimum number of tools needed to answer the question well.

Do NOT perform this chain for every research question:

```text
native search -> exa -> context7 -> gh_grep
```

Examples:

```text
"How does this library option work?"
    -> context7 first

"Show how projects actually use this API."
    -> gh_grep first

"What changed recently in this external service?"
    -> reliable native search, otherwise exa

"Native search result looks suspicious or outdated."
    -> exa or another appropriate source for independent verification
```

Use multiple sources when the claim is important, disputed, security-sensitive, compatibility-sensitive, or likely to have changed.

### Native search and MCP are peers

Do not assume native web search is automatically better merely because the active model provides it.

If native search cannot fetch the required source, gives incomplete results, appears stale, or cannot support an important claim, use the appropriate MCP tool.

Likewise, do not use MCP merely because it exists when native search already provides sufficient authoritative evidence.

### Research must be task-scoped

Do not browse externally when:

- the repository/current worktree already answers the question;
- the active task does not depend on current external information;
- research would only add speculative alternatives outside task scope.

External research exists to resolve uncertainty, obtain current documentation/information, verify claims, or find relevant implementation evidence. It is not permission for unsolicited redesign or dependency churn.

### Documentation research with Context7

When using `context7`:

- identify the actual library/framework and relevant version where practical;
- prefer current documentation applicable to this project;
- verify that examples match the project's language/runtime/version;
- do not copy an API pattern into the project without comparing it against current code and dependencies.

If Context7 and the repository disagree, investigate version/context differences before changing code.

### Real-world code research with gh_grep

When using `gh_grep`:

- treat public repository examples as implementation evidence, not specification;
- prefer maintained/relevant projects where practical;
- never assume a popular pattern is correct for this project;
- verify behavior against official documentation or source when correctness matters;
- do not blindly copy code, architecture, licensing-sensitive content, credentials, or project-specific assumptions.

### General web research with Exa/native search

When using `exa` or native web search:

- prefer official vendor/project documentation for technical facts;
- use recent sources for time-sensitive behavior;
- retrieve the actual source page when snippets are insufficient;
- distinguish verified external facts from inference;
- do not claim external information was verified unless the relevant source was actually inspected.

### Security and privacy

Never send secrets, API keys, access tokens, private credentials, private user data, or unnecessary proprietary source code to external search services.

Search using the minimum information required.

Before sending code fragments externally, prefer abstracting the problem or searching by public API/error/symbol names when that is sufficient.

Repository-local search remains preferred for private implementation details.

### Persist important research across agents

Another model may not have the same native search capability or access to the same search result.

If external research materially affects implementation, planning, debugging, compatibility, security, or a durable project decision, persist the useful conclusion in the coordination layer.

Use:

- `.agent/STATE.md` for currently relevant verified external constraints/facts;
- `.agent/DECISIONS.md` for durable decisions influenced by research;
- `.agent/ISSUES.md` for unresolved external compatibility/API questions;
- `.agent/HANDOFF.md` for research the next agent must know immediately.

Record concise evidence such as:

```text
Source/provider:
Document/page/topic:
Relevant version/date:
Verified conclusion:
Affected project area:
Remaining uncertainty:
```

Do NOT paste large webpages, search dumps, or full documentation into `.agent/`.

Do NOT store private chain-of-thought.

The next model should know the conclusion and how it was verified without repeating expensive research unnecessarily.

### If a research MCP is unavailable

Do not fail the whole task solely because one research MCP is unavailable.

Use the most suitable available alternative:

- another authoritative research tool;
- reliable native search;
- repository-local evidence.

Record a blocker only if current external information is actually required and no reliable source can be reached.

Never fabricate a search result, documentation result, source, or successful MCP call.

---

## 17. Ponytail integration

Ponytail is an additional engineering-discipline layer for this repository.

If the current agent host has the Ponytail plugin/skill/ruleset available, use it. Do not silently disable it.

Ponytail complements this `AGENTS.md`; it does not replace these rules.

### Default mode

For normal implementation, debugging, refactoring, and code-review work:

- keep Ponytail active;
- prefer its normal/default `full` level;
- do not switch it `off` merely to make implementation easier;
- use `lite` only when a lighter mode is clearly appropriate;
- use `ultra` selectively for especially overgrown, complex, or over-engineered work where aggressive simplification is actually useful.

Do not change the user's configured Ponytail mode without a task-related reason.

If the host exposes Ponytail as an always-on plugin, let the plugin inject its rules normally rather than duplicating its full ruleset into project files.

### Use Ponytail during implementation

When designing or implementing a solution, apply Ponytail's simplification ladder after understanding the actual code and task:

1. Does this new thing need to exist?
2. Does the codebase already provide it?
3. Can the standard library provide it?
4. Can the native platform provide it?
5. Can an already-installed dependency provide it?
6. Can the same requirement be satisfied with a materially smaller solution?
7. Only then add the minimum new implementation needed.

Be lazy about unnecessary code, never about understanding the existing code.

Read and trace the affected code before simplifying it.

### Ponytail may not override correctness or preservation

Ponytail's preference for less code must NEVER be used as justification to:

- remove a user-requested feature;
- remove existing behavior that was not part of the task;
- discard uncommitted or unexplained work;
- weaken security, validation, data-loss protection, accessibility, compatibility, or error handling;
- replace a verified implementation with an older/simpler version;
- violate `.agent/DECISIONS.md`;
- ignore acceptance criteria;
- broaden the task into unsolicited cleanup.

"Less code" is valuable only when externally observable requirements and project invariants are preserved.

When simplification conflicts with preservation, correctness, security, compatibility, or explicit user intent, preservation/correctness wins.

### Ponytail review

After a meaningful implementation milestone, especially when the diff is large or introduces new abstractions, wrappers, dependencies, services, helpers, state layers, or duplicated mechanisms, use Ponytail's review capability when available.

Typical capability names may include:

```text
/ponytail-review
@ponytail-review
```

Use the form supported by the current host.

The purpose is to identify unnecessary complexity in the CURRENT diff.

A Ponytail review is advisory. Do not blindly apply its delete-list.

Before applying a simplification:

1. compare it with the task acceptance criteria;
2. compare it with `.agent/DECISIONS.md`;
3. check existing behavior/invariants;
4. inspect the current worktree;
5. ensure it does not remove unrelated or valuable work.

Record any materially important simplification or rejected recommendation in the normal coordination files when the next agent needs to know about it.

### Repository-wide audit

Use Ponytail's repository-wide audit capability only when:

- the user asks for an over-engineering/complexity audit; or
- such an audit is explicitly part of the active plan.

Do not run a whole-repository Ponytail audit during an unrelated feature/fix merely because the skill exists.

Typical capability names may include:

```text
/ponytail-audit
@ponytail-audit
```

### Other Ponytail skills

If available, use specialized Ponytail skills when they directly match the current task, such as review, audit, debt tracking, or related maintenance.

Do not invoke skills mechanically just to satisfy this file. Use them when they improve the task outcome.

### Multi-agent behavior

Ponytail rules should remain active across planner/implementer/fixer/reviewer transitions and subagents when the host supports that behavior.

However, inter-agent continuity must still be recorded in `.agent/`.

Never assume Ponytail carries project state between models.

Ponytail controls engineering style and simplification discipline; `.agent/` carries project facts, decisions, plans, issues, and handoffs.

### If Ponytail is unavailable

Do not stop the task solely because Ponytail is missing.

Continue following this `AGENTS.md` and record nothing as verified about Ponytail activation unless the host actually exposes it.

Do not fake or claim a Ponytail review/audit that was not actually performed.

---

## 18. Skills and specialist workflows

Installed skills are optional specialist tools, not additional authorities.

### General rules

- Use a skill when it materially improves the current task.
- Do not invoke skills mechanically just because they are installed.
- Prefer the smallest relevant set of skills.
- Normally use:
  - one primary specialist skill for creation/design;
  - optionally one separate review/audit skill afterward.
- Do not stack several overlapping design/art-direction skills on the same pass.
- If multiple installed skills overlap, choose the one most appropriate to the current goal.
- A skill must not override:
  - this AGENTS.md;
  - the user's explicit request;
  - current acceptance criteria;
  - existing architectural decisions;
  - worktree safety rules;
  - scope boundaries.

### UI / UX work

For UI work, separate creation from review.

Typical roles:

- UI/UX/design-system skill:
  establish structure, hierarchy, typography, spacing, components and visual direction.
- Art-direction skill:
  use only when a new or substantially different visual concept is requested.
- Anti-slop / polish skill:
  use after implementation for critique and bounded refinement.
- Motion skill:
  use only when animation or microinteraction work is in scope.
- UX/product review skill:
  use for planning or explicit UX review, not automatically during implementation.

Do not let multiple art-direction skills independently redesign the same interface in one pass.

### Skill selection

If the user explicitly names a skill, prefer that skill when available.

Otherwise choose based on the task rather than activating every applicable skill.

Examples:

- new visual concept -> one art-direction/design skill
- normal UI implementation -> one UI/design-system skill
- existing UI cleanup -> one audit/polish skill
- animation review -> one motion skill
- product/UX critique -> one UX/review skill

### Completion

Using a skill does not expand the task scope.

When the requested work and acceptance criteria are complete:
- do not invent additional redesign or polish work;
- record optional findings as follow-up/technical debt if useful;
- perform required verification;
- stop the current task.

---

## 19. Long-session protection

Long sessions may lose details through context growth, summarization, compaction, or model switching.

Therefore:

- update coordination files after milestones, not only at session end;
- explicitly record important UI/behavior invariants;
- record unfinished work and sensitive files;
- keep `HANDOFF.md` fresh;
- checkpoint verified milestones when safe;
- never assume absence from memory means absence from the project.

If conversation memory conflicts with the repository, inspect the repository.

---

## 20. Completion protocol

Before reporting substantial work complete:

1. inspect the relevant current diff;
2. run practical verification;
3. check for unrelated regression/removal;
4. update `PLAN.md`;
5. update `STATE.md`;
6. update `ISSUES.md`;
7. update `DECISIONS.md` if needed;
8. rewrite `HANDOFF.md`;
9. create a safe local checkpoint if appropriate;
10. report only verified results and remaining uncertainty.

A future agent must not need your final chat message to reconstruct project state.

---

## 21. Keep coordination memory small

Do not put full transcripts, hidden reasoning, huge logs, or complete source files into `.agent/`.

Use concise facts and references:

- paths;
- symbols;
- commit hashes;
- commands;
- test results;
- decisions;
- acceptance criteria;
- next actions.

The coordination layer should reduce context cost, not become another giant context.

---

## 22. Project-specific rules

Add durable repository-specific rules below.

### Commands

```text
Build: <fill when known>
Test:  <fill when known>
Lint:  <fill when known>
Run:   <fill when known>
```

### Project invariants

- <add important behavior that must not regress>
