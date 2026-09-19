# Payment (Receipt Entry) — Migration Mapping (eBizWiz → EdifyBiz)

> **Superseded (17/09/2026)** — first draft of 15/09/2026, kept for history only. The implemented and verified mapping is **`13_payment_field_mapping.md`**. Its §3 warning ("receipts without a module become false advances") was right; it is resolved in 13 by billing AMC and call bills as Sales Invoices and allocating the receipts to them.

Client module **Payment Receipt Entry** (`/Receipts/receipt.aspx`). Read as: what the client sees → where it sits in the client DB → which column it goes to in our DB.

EdifyBiz target = the **Payment** module (`/edify/payment/`, backend `app/payment.asp` action `addpaymentinout`, JS `assets/scripts/payment.js`). A receipt = one `payments` row (`inout = 0`); the split against invoices = `paymentdetails` rows (`paycode` → `payments.code`). **No form changes** — only fields the existing Payment form already has.

Source tables: `trhpayrg` (receipt header), `trdpayrg1details` (receipt lines — one line per bill paid). Scope: offices 2/3/4/6.

**Verified against source (2026-09-15):** 3,250 receipts, 3,565 lines. Every line says what it pays (**Payment Is**):

| Payment Is | Lines | Receipts | Points to (`ntrnno` = bill `ncode`, same office) | In EdifyBiz today? |
|---|---|---|---|---|
| SALES | 697 | 696 | `trhsales` (Sales Invoice) — 697/697 match | Yes (module 07) |
| AMC | 2,429 | 1,726 | `trdcontr3bills` (AMC contract bill) — 2,429/2,429 match | No — AMC Contract pending |
| CALLS | 438 | 418 | `trhcalls` (Call Entry) — 438/438 match | No — Complaint pending |
| — mixed | — | 1 | one receipt pays SALES + AMC | — |
| — no lines | — | 409 | receipt header only, no amount anywhere | — |

---

## 1) Receipt header → `payments`

| Client UI (eBizWiz) | Client DB | Our DB (`payments`) |
|---|---|---|
| Trn. No. | `trhpayrg.vtrnprefix` + `ntrnno` | `paymentno` |
| Trn. Date | `trhpayrg.dtrndate` | `date` |
| Party | `trhpayrg.nparty` → target `contact` (by name) | `ccode` |
| Deposit Bank | `trhpayrg.ndepositbank` → `mstbanks` (BOB 2,994 / blank 256) | `bankcode` → `companybank` (see §4) |
| Amount Recd. | sum of line `nchequeamount` (header has no amount column) | `amount` |
| Pay Mode | line `npaymode`: NEFT/RTGS 3,208 · CHEQUE 347 · DD 6 · CASH 3 | `mode` = `neft` / `Chq` / `DD` / `Csh` |
| Cheque / UTR No. | line `vchequeno` | `instno` |
| Cheque Date | line `dchequedt` | `instdate` |
| Bank Drawn On | line `nbankdrawnon` → `mstbanks.vname` | `instbank` (text) |
| Remarks | `trhpayrg.vremarks` + line `vremarks` | `remark` |
| Ref. No. / Ref. Date | `trhpayrg.vrefno` / `drefdate` | `remark` (folded — no ref field on the Payment form) |
| — | — | `inout` = 0 (receipt), `module` = `Sal` for SALES receipts |

Notes:
- 307 receipts have more than one line; only 7 use different cheque numbers and 3 different modes. `mode`/`instno`/`instdate` come from the first line; any other cheque no. is noted in `remark`.
- WCT (4 lines) and Other deduction (8 lines) have no column → noted in `remark`.
- Terms & Cond. (`nterms` / `vtermsconditions`) is blank in source → not migrated.

---

## 2) Receipt lines → `paymentdetails` (SALES)

| Client UI | Client DB (`trdpayrg1details`) | Our DB (`paymentdetails`) |
|---|---|---|
| Bill (Sales Invoice) | `ntrnno` → `trhsales.ncode` → migrated `sal_order.code` | `modulecode` |
| Amount received | `nchequeamount` | `amountwithouttds` |
| TDS deducted | `ntdsdedt` | `tds` |
| — | received + TDS | `amount` |

- Verified: **received + TDS = bill total** (e.g. SA2246: 5,568 + 96 = 5,664) — same meaning as EdifyBiz `amount = amountwithouttds + tds`.
- All 697 SALES lines resolve to a migrated invoice; receipt party = invoice customer in 696 (1 differs in source too).
- If a receipt's amount is more than its lines, the Payment module keeps the balance as an extra `paymentdetails` row with `modulecode` NULL (on-account). Same here.

---

## 3) AMC and CALLS receipts — depend on modules not migrated yet

- In EdifyBiz an AMC bill is billed through `sal_order`, and its payment is a `module='Sal'` payment on that invoice (see `app/reports/amccontract.asp`). So AMC receipts can only be linked correctly **after AMC Contract is migrated** (its bills → `sal_order`).
- CALLS receipts need the Complaint (Call Entry) module first.
- **They must NOT be migrated as receipts without a module:** EdifyBiz treats `payments` with empty `module` + `inout=0` + `paid=1` as the customer's **advance** (offered against new invoices in Sales). 2,145 AMC/CALLS receipts would become false advances.

---

## 4) Deposit bank → `companybank`

`companybank` is **empty** in the target. Source `mstbanks` has BOB (our bank, `bourbank=1`, a/c 1234567890, Malad) and SBI (not our bank). Seed BOB (and SBI) into `companybank` (bank, accountno, branch) so `payments.bankcode` has something to point to. Receipts with a blank deposit bank (256) keep `bankcode` empty.

---

## 5) Decisions needed before the exe

1. **AMC (1,726) + CALLS (418) receipts:** migrate now as SALES-only, and do AMC/CALLS receipts after AMC Contract / Complaint are migrated (recommended — keeps the links right and avoids false advances)? Or migrate them now with a non-empty `module` tag and the bill ref in `remark`?
2. **409 receipts with no lines and no amount:** skip (recommended — nothing to receive), or keep as amount 0 with ref/remarks?
3. **Cleared status:** EdifyBiz sets `paid = 1` only for Cash or when **Bank Reco Date** is filled; `paid = 1` is what reduces invoice outstanding. eBizWiz only records money already received. Proposed: `paid = 1`, `bankricodate` = cheque/receipt date.
4. **Invoice outstanding note:** outstanding = invoice lines + `sal_tax` − payments. Tax was not migrated (module 07), so on a migrated invoice the received amount (tax-inclusive) is larger than the line total — the invoice shows as fully paid (no outstanding), not as a true balance.
5. **Payment No.:** keep the eBizWiz number (`PA1998`) in `paymentno`; auto-number for Payment is off in the target.
