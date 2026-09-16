# AMC Contract — Migration Mapping (eBizWiz → EdifyBiz)

Client menu **Sales → Contracts → Contract Sales** (`trhcontr`, number prefix `MC`). Read as: what the client has → where it sits in the client DB → which table/column it goes to in our DB.
**Status: approved 2026-09-15 — exe module 10 "AMC Contract" built; no form change (see Build status).** Checked in the client DB, our DB and the project code (`amc/app/contract.aspx.cs`, `amc/contract/default.asp`, `amc/scripts/contract.js`, `amc/app/complaint.aspx.cs`), plus the Cona reference DB.

Source (offices 2/3/4/6): `trhcontr` 4,528 headers · `trdcontr1items` 5,283 product lines · `trdcontr2itemsdet` 7,187 serial rows · `trdcontr3bills` 7,618 bills (4,520 contracts) · `trdcontr7pmvisit` 14,219 PM visits · `trdcontr4posttaxchgs` 6,012 charges (3,858 contracts, 3,993 GST rows) · `trdcontr5tech` 0 · `trdcontr6checks` 0.

---

## 0) EdifyBiz AMC Contract (standard, saksham = generic path)

| Screen part | Table | Note (from code) |
|---|---|---|
| Contract header | `amc` | form `amc/contract/default.asp`, save `SqlOperation("amc")` |
| Equipment lines | `contractdetails` | one row per serial (`srno` free text), `amount`, `intervals` = PMS visits per year (0/1/2/3/4/6/12) |
| PMS visits | `contractcall` `type='PMS'`, `contractdtcode` = `contractdetails.code` | app creates them from `intervals`; status from `status` module `'AMC'` |
| Billing schedule | `billingcycle` (`amccode`, `billingno`, `billingdate`, `amount`, `type='BILL'`, `salcode`) | Billing Cycle **tab hidden for saksham**; list payment status reads it |
| Link to quotation | `amc.module='CQ'`, `amc.modulecode = inqcs.code` | view shows the quotation no. |
| Renewal | `amc.oldcontractcode` (+ old `renew = 1`) | same as the Renew button |
| Tax | none on the contract | tax appears only when billed as a Sales Invoice |

Masters in our DB: `amctype` = 1 Comprehensive · 2 Non-Comprehensive · 3 Warranty (stored as the code text, e.g. '1'); `amcplan` empty; **`status` module 'AMC' empty** — the app looks up `name='Open'` and `behavior='Completed'` (Cona: Open/Pending, In-Process/Pending, Closed/Completed). Autonumber 'AMC Contract' not configured.

⚠ `amc.salcode` must **not** be set to a migrated invoice: `deleteSalesEntry` (`contract.aspx.cs` ~5296) deletes the linked `sal_order` + lines + tax + adjustments whenever the contract header or a line is edited.

---

## 1) Header → `amc`

| Client (eBizWiz) | Client DB (`trhcontr`) | Our DB (`amc`) | Note |
|---|---|---|---|
| Trn. No. | `vtrnprefix` + `ntrnno` | `contractno` | `MC…` |
| Trn. Date | `dtrndate` | `contractdate` | |
| Party | `nparty` → `contact` | `ccode` | required on form |
| (party branch) | default `mltaddress` | `contactbranch` | |
| Party Contact | `npartycontact` → `mltcontact` | `contactperson` | 3,996 |
| Sales Person | `nsalesman` → `users` | `executive` | 4,023; fallback Admin |
| Contract Type | serial `ncontrtype` (COMPREHENSIVE 1,166 · NON-COMPREHENSIVE 6,020) | `amctype` = '1' / '2' | 12 contracts mix both → type of the most serials, each line's type in its remark |
| Start / End | serial `dstartdate` / `denddate` (same on all serials in 4,501 of 4,521) | `startdate` = earliest, `enddate` = latest | |
| Period | serial `nmonths` (12 = 6,475 · 36 · 24 · 6 · 60 …) | `contractyears` = months ÷ 12, `contractmonths` = remainder | |
| AMC Quotation No. | `namcquoteno` → migrated AMC quotation (branch + `QA` no.) | `module='CQ'`, `modulecode` = `inqcs.code` | 4,165 of 4,528 |
| Previous contract (renewal) | line `ncontractis` = TRANSFER FROM CONTRACT, `npreviousno` → `trhcontr` | `oldcontractcode` = migrated `amc.code`; that old contract `renew = 1` | 3,394 of 3,627 found |
| P.O. No / Date | `vpono` / `dpodate` | `ponum` / `podate` | 21 |
| Payment Schedule | `npaymentschedule` → `mstpaymentschedule` + `vbegorend` | `paymentterms` (text) | 4,528 |
| (office) | `nofficeid` | `branch` | required |
| Remarks + Comment + Ref No/Date + item total / total incl. tax + charges (GST …) + amount received | `vremarks`, `vcomment`, `vrefno`, `drefdate`, `nitemtotal`, `ntotalamount`, `trdcontr4posttaxchgs`, `namountrecd` | `remarks` (labelled) | no tax on the standard contract |
| — | — | `serviceengineer`, `department`, `visitwith`, `intervals`, `salcode` = NULL | eBizWiz has no service engineer on the contract |

Not migrated: `ntrhcampa` (0), `nsalessource` (1), `ncheckset` (no check rows), `vtermsconditions` (0), `vletterhead` (print), approval (all approved; `amc` has no approval column).

---

## 2) Lines → `contractdetails` (one per serial)

| Client | Client DB (`trdcontr2itemsdet` + `trdcontr1items`) | Our DB (`contractdetails`) |
|---|---|---|
| Item | `trdcontr1items.nitem` → `product` | `productcode` |
| Serial No. | `vserialno` (7,187 / 7,187) | `srno` |
| Rate (net) | `nrate` (master rate / discount % only in remark — no discount column) | `amount` |
| Location | `vlocation` (5,602) | `location` |
| First installation | `dfirstinstdate` (2,271) | `installationdate` |
| Warranty → AMC (TRANSFER FROM WARRANTY, 845 point to a Sales Invoice) | `npreviousno` → `trhsales` | `invoiceno` = `SA…`, `invoicedate` = invoice date (standard line fields the app fills when a contract comes from a sales invoice) |
| Period of this serial | `dstartdate`, `denddate` | `startdate`, `enddate` |
| PM visits | `npmvisits` over `nmonths` | `intervals` = visits per year when it is one of 0/1/2/3/4/6/12, else NULL (actual visits come from §4) |
| Contract type, months, PM visits, closed (39), master rate / discount %, sign-up/renewal/transfer | `ncontrtype`, `nmonths`, `npmvisits`, `bclosed`, `nmasterrate`, `ndiscountperc`, `ncontractis` | `remark` (labelled) |
| — | — | `quantity` = 1, `iscomponent` = 0, `contractplan` / `dealercode` NULL |

---

## 3) Bills → `billingcycle`

| Client DB (`trdcontr3bills`) | Our DB (`billingcycle`) |
|---|---|
| `ncontr` | `amccode` |
| `nbillno` (bill number of the contract; **not** a sales invoice — amount equals the invoice with that code on only 4 of 3,640) | `billingno` = `BILL_<contract no>_<n>` (app format) + eBizWiz bill no. kept |
| `dbilldate` | `billingdate` |
| `nbillamount` | `amount` |
| — | `type='BILL'`, `salcode` NULL |

`namountrecd` (2,389 bills) = money received → the **Payment** module (eBizWiz Receipt Entry refers to these bills via `trdpayrg1details.nbillno`).

---

## 4) PM visits → `contractcall` (`type='PMS'`)

| Client DB (`trdcontr7pmvisit`) | Our DB (`contractcall`) |
|---|---|
| `ncontr2` → the serial row → its `contractdetails` | `contractdtcode` |
| `dschpmdate` (scheduled) | `date`, `complaintdate` |
| `dactpmdate` (done, 11,502) | `status` = Closed + `closedt`; not done → Open |
| (contract / serial) | `ccode`, `productcode`, `serialno` |
| — | `pmscomplaintno` = `PMS<detail code>_<n>` (app format), `type='PMS'` |
| `ncalls` (12,930 → `trhcalls`) | remark "Call: …" now; linked when Call Entry is migrated |

Requires AMC statuses (D1).

---

## 5) Decisions for your confirmation

| # | Decision | Why | Needs |
|---|---|---|---|
| D1 | Seed `status` module 'AMC': **Open** (behavior Pending, default) and **Closed** (behavior Completed) — same names/behaviors the app code looks for (and Cona uses) | PMS/complaints can't show or close without them; empty in our DB | exe |
| D2 | Do **not** set `amc.salcode`; warranty-transfer lines get `invoiceno`/`invoicedate` | editing a contract with `salcode` deletes that sales invoice | exe |
| D3 | GST/charges and totals → contract `remarks` | the standard AMC contract has no tax/adjustment table; tax comes when billed | exe |
| D4 | Bills → `billingcycle` (data kept). The Billing Cycle tab is hidden for saksham (`amc/contract/default.asp:1204` shows it only for Zinq, ZinqUat, Skytech, enliten, medispec, demo; the same `If` block also holds the Invoice tab) — add `saksham` to that condition? | to see the bill schedule on screen | exe (+ optional 1-line form change) |
| D5 | PM visits → `contractcall` PMS with real dates/status (not regenerated) | eBizWiz schedule + done dates are real data | exe |
| D6 | Received amounts on bills → Payment module | receipts are their own module | later |

No other form change needed.

---

## Build status (2026-09-15, "go ahead")

- **exe:** `AmcContract` class in `Program.cs`, menu **10. AMC Contract** (after AMC Quotation in Run ALL). D1 statuses `Open`/Pending (default) + `Closed`/Completed seeded under module 'AMC'; header `amc` (module 'CQ' → AMC quotation by branch + QA no.); `contractdetails` per serial (warranty-transfer lines `invoiceno`/`invoicedate` = SA no./date, `salcode` never set); `billingcycle` per bill ordered by date, `billingno` = `BILL_` + right-10 of contract no. + `_n` (app format, `contract.aspx.cs:2131`); PMS `contractcall` per PM visit, `pmscomplaintno` = `PMS` + 7-digit detail code + `_k` (app format), status Closed when done; renewals linked after all contracts are in (`oldcontractcode`, previous `renew = 1`).
- **D4 not done as a form change:** re-reading `amc/contract/default.asp`, the Zinq/Skytech/enliten/medispec/demo `If` at line 1204 wraps **Billing Cycle, Invoice, All Complaints, All PMS and Expense** tabs, and the pane block at 1810–1988 is gated the same way. Adding saksham there opens 5 tabs, not 1 — so no form change was made; bills are in `billingcycle` (data kept) and showing the tabs is a separate decision.
- Payment link for later: eBizWiz receipts point to contract bills (`trdpayrg1details.nbillno`); the exe orders bills by `dbilldate`, `ncode`, so the Payment module can map bill → `billingcycle` row the same way.

## Client form re-check (Contract Entry screen, 2026-09-15)

Field-by-field against the eBizWiz **Contract Entry** screen: Trn. No./Date, Party, Party Contact, Sales Person, AMC Quote No., Pymt. Schedule, B / E of Period, Campaign, Sales Source, P.O. No./Date, Terms & Cond., Check List, Ref. No./Date, Remarks, Comment, Letterhead, Trn. Total — all covered. Added after the check:

| Client field | Client DB | Our DB |
|---|---|---|
| Pymt. Schedule + **B / E of Period** | `npaymentschedule` (Half Yearly 1,494 · Yearly 1,412 · Advance 1,308 · Quarterly 247 · Contract Payment 53 · Once In 4 Months 13 · Monthly 1) + `vbegorend` B 2,087 / E 2,441 | `paymentterms` = "Half Yearly - Beginning of Period" (B/E shown as the form's text, not the letter) |
| **Terms & Cond.** | `nterms` → `msttermset` ("ESCO Standard Terms and Conditions", 741) | `remarks` "Terms & Cond.: …" (`amc` has no terms column) |
| **Check List** (template chosen) | `ncheckset` → `mstcheckset` (2,557; no check items exist, `trdcontr6checks` = 0) | `remarks` "Check List: …" |
| Campaign / Sales Source | `ntrhcampa` 0 / `nsalessource` 1 | not migrated |
| Letterhead | print only | not migrated |

Same three text fixes applied to the AMC Quotation module (09): B/E text in the payment-schedule remark, `nterms` 1,436 and `ncheckset` 6,845 template names in `remark`. **Code changed after the Run-ALL that is currently running started → needs the next rebuild + rerun.**

## Verified — Run ALL 2026-09-15 (10 modules, 0 errors) + fixes after it

- AMC Contract: 4,528 contracts, quotation link 4,165 (party same as quotation on 4,156), `salcode` 0, B/E text 4,528, Terms 741, Check List 2,557; `contractdetails` 7,186 (1 eBizWiz serial row has no product line); billing 7,618 = ₹22.61 Cr; PMS 14,219 (Closed 11,502 with `closedt`, 0 orphans, all numbers unique).
- AMC Quotation (09): 8,758 Approved, status on all, total = eBizWiz on 8,696 of 8,714 (18: source total 0 while charges exist), check list 25,833 (`CQ`), `pdesc` S/N + period + contract / previous doc.
- Sales Invoice labels (07): Warranty Sales 4,313 + Non Warranty Sales 1,394, exactly one label per invoice.
- **Fixed in exe after this run (needs rerun):**
  1. **Serial → contract by the serial's own `ncontr`** (was by its product line). 20 eBizWiz serial rows point to a product line of another contract; the header `nitemtotal` follows the serial's `ncontr` (e.g. MC1395 21,630 / MC1397 43,260 were swapped). Contract amount matched on 4,499 of 4,528 before the fix.
  2. **Renewals:** 296 eBizWiz contracts renew several old contracts → every previous contract now gets `renew = 1`; `oldcontractcode` = first previous; each line remark carries "Previous Contract: MC…". 10 self-referencing renewals ignored.
- Known, kept: `billingno` repeats across offices (4,786 distinct of 7,618) because MC numbers restart per office (1,587 numbers exist in two offices) and the app format uses only the contract number.

_Re-verified after the fix (Run ALL 2026-09-15, 10 modules, 0 errors): contract amount = Σ serial rates of the serial's own contract on **4,528 of 4,528** (= header `nitemtotal` on 4,480; the other 48 eBizWiz headers are stale), MC1395 21,630 / MC1397 43,260 now correct; previous contracts with `renew = 1` 3,308 of 3,309; renewed contracts with `oldcontractcode` 2,929 of 2,932 (the 3 have their renewal only on a product line whose serials sit in another contract); 4,625 lines carry "Previous Contract: MC…"; PMS orphans 0; `salcode` 0._
