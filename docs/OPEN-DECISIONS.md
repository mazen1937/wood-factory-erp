# Open Decisions Register

These items are deliberately unresolved. Code must use a configurable/effective-dated design, explicitly block the action, or record a resolution state. Do not silently choose a rule.

| Decision | Safe current behavior | Required confirmation |
| --- | --- | --- |
| Formal double-entry general ledger and chart of accounts | Track integrated financial events without claiming a full GL exists. | Accountant/project owner. |
| Egyptian tax, statutory invoice and e-invoicing rules | Keep tax/document model configurable; do not hardcode rates or legal formats. | Egyptian accountant/legal review. |
| Direct labor versus pooled wages | Explicit treatment and duplicate-cost prevention. | Factory/accountant pilot validation. |
| Allocation timing, rounding and final zero-denominator settlement | Versioned monthly run; flag/carry zero-denominator pools. | Accountant approval. |
| Stage-level WIP attribution | Track actual linked costs and accepted quantities. | Factory pilot. |
| Invoice timing, numbering and grouping | Support linked sales/order documents without duplicating stock or revenue. | Factory/accountant. |
| Negative-stock exception | Block ordinary negative stock. | Named policy and approver. |
| Scrap/waste treatment | Store configurable, traceable records; no automatic financial behavior. | Current factory workflow. |
| Meaning of the `فيترن` register and duplicate pricing sheets | Preserve as migration evidence only. | Factory owner. |
| External processing/subcontracting | Do not add as a current workflow merely because historic sheets mention it. | Current factory confirmation. |
| Exact production/engineering file fields | Support attachments and revisions plus structured BOM/cut list where supplied. | Factory/engineer workflow review. |
