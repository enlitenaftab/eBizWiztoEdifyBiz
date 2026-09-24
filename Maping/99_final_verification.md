# Final verification — latest Run ALL (17 modules, 0 errors)

Source `saksham70v1_1` (offices 2, 3, 4, 6) → target `SakshamRMtP15329`. Module-level client-vs-ours checks are in each `NN_*.md`; this page is the cross-module check of the final DB.

## 1. Modules

| # | eBizWiz (client) | EdifyBiz (ours) | Result | Mapping |
|---|---|---|---|---|
| 1 | Master > Party | Contact | 20,756 contacts, 83,629 persons; 255 users given branch access; 104 users inactive in eBizWiz suspended; Party Type 10 / Profile 40; designations 7,677; 4 branches with address / phones / zone / Office Type | 01 |
| 2 | Master > Item | Product | 29,966; item type labels 19; currencies 13 + 7 conversion rates; every document line linked to its product by Item Code | 02 |
| 3 | Inquiry > Inquiry Entry / Offer Request + Follow-up | Inquiry CI + Follow Up | 193,337 inquiries, 521,948 follow-ups, 31,819 check items; competitors → remark on 6 | 03 |
| 4 | Quotation > Quotation Entry | Inquiry CQ | 110,493, 473,641 lines, 271,700 check items | 04 |
| 5 | Order > Order Received | Inquiry SO | 7,658, 35,731 lines, 149,662 check items | 05 |
| 6 | Order > Order Placed | Purchase Order | 6,031, 31,362 lines, 82,814 check items | 06 |
| 7 | Sales > Warranty / Non Warranty Sales | GST Sales > Sales Invoice | 5,707, 18,693 lines, tax on 2,063 | 07 |
| 8 | Stock > Stock-IN / Stock-OUT (+ Item Opening Balance) | Inventory GRN / MRO / GTA + Opening Stock | 6,433 / 559 / 767; opening stock 2,589 JV; eBizWiz balance adjustment 2,501 JV (end of module 11); stock = eBizWiz balance on 4,352 / 4,352 item + branch; summary = ledger on all 8,569 rows | 10 |
| 9 | Contracts > AMC Quotation | Inquiry CQ (AMC Contract) | 8,758, 13,541 lines; schedule bills in terms on 1 | 09 |
| 10 | Contracts > Contract Sales + Schedule Bills + warranty | AMC Contract + billing cycle Sales Invoices + Warranty | 4,528 contracts, 7,618 billing cycles, 3,996 bill invoices (₹11,70,73,427.85), 4,261 warranty contracts | 11, 13 |
| 11 | Service > Call Entry (+ call bill) | AMC Complaint (+ call bill Sales Invoice) | 36,164 calls, 56,248 visits, 224,583 check items, 960 call invoices (₹2,41,96,132.26) | 12, 13 |
| 12 | Receipts > Receipt Entry | Payment | 2,880 payments ₹18,10,20,531.23, 3,565 allocations, on account ₹0 | 13 |
| 13 | Service > Customer Feedback / Survey | Complaint > Customer Rating | 3,420 | 14 |
| 14 | Marketing & Target > Sales Target on User | Target | 2,856 monthly rows = ₹6,41,80,07,001 | 15 |
| 15 | Marketing & Target > Campaign Entry | Mass Mail | 8 (header only, no recipient) | 15 |
| 16 | Master > Expenses | Expense | 3 expenses, 2 lines ₹1,250, 1 Sanctioned | 15 |
| 17 | Task (`trdtask`) | Task | 5 tasks + 5 tickets | 15 |

## 2. Cross-module DB sweep

| Check | Result |
|---|---|
| Orphans (20 checks: lines without header, check lists / follow-ups / feedback / tickets / expense lines without their document, billing cycle → missing invoice, payment → missing invoice, call → missing contract line) | **0 on all** |
| Contract calls without date or status; payments / contracts without customer; sales invoices without customer or branch; targets not on the 1st | **0** |
| Quotations / orders without customer | 5 (CQ QT19187; SO OR4, OR771, OR772, OR1500) — no party in eBizWiz either (04 / 05) |
| Purchase orders without supplier | 1 (OP1856) — no party in eBizWiz either (06) |
| Duplicate call / contract / PO / CI numbers | **0** |
| Sales invoice number used twice in one branch | 11 — each is a call bill that has **no prefix in eBizWiz** (e.g. "512") and shares the number with an AMC bill of the same branch; both are the client's own numbers, told apart by the labels "Service Call Bill" / "AMC Contract Bill" and the remark |
| Stock summary vs ledger | 8,569 rows, **0** differences |
| All invoices (10,663): total ₹1,75,65,51,896.94, received ₹18,27,03,999.47 | over-paid **2** (the same 2 AMC bills are over-paid in eBizWiz) |
| Customer Advance created by the migration | **₹0** |

## 3. Forms and views (saksham-gated changes)

| Screen | What saksham sees | Mapping |
|---|---|---|
| Inquiry CI / CQ / SO | the saksham form ports (CI / CQ / SO fields, Task Check List tab); the ported fields are on the views too | 03, 04, 05 |
| Purchase Order | the saksham form port (9 fields), also on the view | 06 |
| Sales Invoice | the saksham form port (8 fields) and the saksham block, also on the view | 07 |
| AMC Contract | contract tabs + Product Desc. column | 11 |
| AMC Complaint | Task Check List tab + Customer Rating block | 12, 14 |
| Bill To / Ship To | labelled **Customer Billing Branch** / **Customer Shipping Branch** on the form and the view in Inquiry (CI / CQ / SO / AMC quotation), Purchase Order and Sales Invoice, and on the AMC Contract view | 03–07, 09, 11 |

- Where Bill To / Ship To show on the views: Inquiry CI / CQ / AMC quotation (Bill To), SO (Bill To; Ship To with party and address), Sales Invoice (Bill To / Ship To), Purchase Order (Bill To / Ship To), AMC Contract (Bill To). Contact shows them as address columns; Complaint shows Contact Branch.
- Contact, Product and the modules without saksham form changes use the standard screens.
- JS syntax (parse) is OK on `assets/scripts/inquiry.js`, `gst/purchase/script/purchaseorder.js`, `gst/sales/script/sales.js`, `amc/scripts/contract.js`, `amc/scripts/complaint.js`. The saksham gates are in `app/inquiry.asp`, `inquiry/default.asp`, `gst/sales/default.asp`, `gst/sales/app/sales.asp`, `gst/purchase/default.asp`, `amc/contract/default.asp`, `amc/complaint/default.asp`, `contact/default.asp`, `product/default.asp`. Inventory GRN / MRO / GTA use the standard (ungated) check-list tab.

## 4. Check lists

- The exe writes no check-set name into any remark (CI, CQ, SO, PO, SI, AMC quotation, AMC contract, complaint). `Check List:` left in remarks: **0** in PO, CI / CQ / SO / AMC quotation, SI, AMC contract and calls.
- The eBizWiz check sets are seeded as the standard **Checklist Master** (`taskchecklistmaster` / `taskchecklistmasterdet`): **24 sets, 253 items** = the sets of offices 2/3/4/6 that are active or used (set 1 "CHECK LIST" is inactive and unused in scope, so it is left out by rule).
- Document check items stay in the Task Check List tab: ACM 224,583 · CI 31,819 · CQ 297,533 · PO 82,814 · SAL 56 · SO 149,662.

## 5. Bill To / Ship To data behind the views

| Document | Bill To | Ship To |
|---|---|---|
| Inquiry CI | 193,338 / 193,338 | — |
| Inquiry CQ + AMC quotation | 119,251 / 119,252 (1 = no party in eBizWiz) | — |
| Inquiry SO | 7,656 / 7,659 | 7,456 with party |
| Sales Invoice | 10,663 / 10,663 | 4,675 (sales invoices only — AMC / call bills have none) |
| AMC Contract | 8,789 / 8,789 | — |
| Purchase Order | 4,952 | 4,933 (where the client gave them) |

## 6. Not migrated (no data or out of our standard)

| eBizWiz | Why |
|---|---|
| Upload Doc (DMS, 284,599 file references) | skipped by the user; the files are on the client server |
| Sales Target on Office (7) | no office target in our standard (14) |
| Claim Form, Write Off, Assign Service Routes, user routes | 0 rows |
| Physical Stock Taking (5), user stock transfer (1) | headers without lines |
| Quotation Technical Set (3) | all values empty |
| Action / print / report screens (Reschedule PM, Sign-Up / Renewal Advice, Range Prints, Call Sheets, Re-Allocation, Call Allocation, Search Serial, Global Pending Calls) | no data of their own |
| WinMax test offices 1 and 5 | out of scope everywhere |

## 7. Deploy note

The local-only patches (`App_Code/include.cs`, `App_Code/aspSession.cs`, `web.config` en-US culture) must **not** go to production.
