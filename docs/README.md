# Wood Factory ERP Documentation

This folder is the version-controlled source of truth for project decisions and implementation rules.

## Start here

- [Project contract](PROJECT-CONTRACT.md): approved architecture, domain model, transaction rules, financial controls, and delivery order.
- [Open decisions](OPEN-DECISIONS.md): decisions that must not be guessed in code.
- [Project status](PROJECT-STATUS.md): what is implemented and the next work item.
- [Source contracts](reference-contracts): original Word documents retained for exact historical wording and tables.

## Rule of precedence

1. A confirmed rule in `PROJECT-CONTRACT.md`.
2. A decision subsequently recorded in this repository.
3. A configurable rule with an approved effective-dated setting.
4. An explicit open decision: block or raise a resolution state; never silently invent a behavior.

The Excel workbooks are migration evidence, not live ledgers or formula sources. The server and PostgreSQL are the only authoritative source for official ERP postings.
