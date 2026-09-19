# 13. Receipt Entry → Payment — field mapping

## 1. Overview

| | Client (eBizWiz) | Ours (EdifyBiz) |
|---|---|---|
| Screen / menu | Receipts → Payment Receipt Entry (header + "Receipt Detail" grid) | Payment (`/edify/payment/`) |
| Tables | `trhpayrg` (3,250) · `trdpayrg1details` (3,565) · masters `mstbanks`, `mstfixedselection` (Payment of / Payment Mode) | `payments` · `paymentdetails` · `companybank`; bills billed as Sales Invoices `sal_order` / `sal_order_det` / `sal_tax` / `sal_adjust` + `billingcycle.salcode` |
| Code | — | form + view `payment/default.asp`, JS `assets/scripts/payment.js`, backend `app/payment.asp` (`addpaymentinout`, `fillInvoice`); AMC link `amc/app/contract.aspx.cs` (`updateBillingCycle`, `LinkBillingCycleWithInvoice`, `getOutStandingAmount`) |
| Exe | — | module **12. Payment (Receipt Entry)**, class `Payment`; bill invoices by `AmcBillInvoice` (module 10, after `WarrantyContract`) and `CallBillInvoice` (module 11, after `Complaint`); both reuse the Sales Invoice GST helpers of `SalesInvoice` (`PrepareExternalTax`, `ApplyTaxFor`, `SalesLabelFor`, `InrCode`) |
| Scope | offices 2, 3, 4, 6 (1 and 5 are WinMax test offices) | branches 3, 4, 5, 6 |
| Status | | migrated and verified |

One receipt line in eBizWiz pays one document ("Payment of" = SALES / AMC / CALLS). The cheque sits on the **line** in eBizWiz and on the **header** in EdifyBiz, so one `payments` row is written per cheque and one `paymentdetails` row per line. Every line is allocated to a Sales Invoice: the migrated invoice (SALES), the AMC bill invoice (AMC) or the service call bill invoice (CALLS).

---

## 2. Field mapping

### 2.1 Header

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 1 | Trn. No. | `trhpayrg.vtrnprefix` + `ntrnno` | `payments.paymentno` | Payment Number | — (list column "Payment No.") | e.g. `PA2002`; a receipt with several cheques → `PA2002-1`, `-2`…; auto-number for Payment is off, nothing overwrites it |
| 2 | Trn. Date | `dtrndate` | `date` | Date | Date | |
| 3 | Party | `nparty` | `ccode` | Contact | Contact | party → contact (module 1) |
| 4 | Deposit Bank | `ndepositbank` → `mstbanks` | `bankcode` → `companybank` | Bank/Cash | Bank/Cash | 2 banks seeded (§3); 195 receipts have no deposit bank → NULL |
| 5 | Amount Recd. | Σ line `nchequeamount` of that cheque | `amount` | Amount | Amount | header has no amount column in eBizWiz |
| 6 | Ref. No. / Ref. Date | `vrefno`, `drefdate` | `remark` "Ref No: … dt. …" | Notes | — | |
| 7 | Remarks | `vremarks` | `remark` (first part) | Notes | — | `remark` holds 1,000 characters (`nvarchar(2000)`) |
| 8 | Terms & Cond. | `nterms`, `vtermsconditions` | — | — | — | 0 rows |
| 9 | — | — | `inout` = 0, `paid` = 1, `bankricodate` = receipt date | Bank Reco Date | Bank Reco Date | the app counts a receipt against an invoice only when `paid = 1` |
| 10 | — | — | `module` | Module | — (list column "Against") | `Sal` when any line of that cheque is allocated to an invoice |
| 11 | — | — | `remark` "Receipt No: PA… \| office" + per line "Sales Invoice: …" / "AMC Contract: MC… \| Bill No: … dt. …" / "Complaint: … \| Bill No: …" | Notes | — | `paymentdetails` has no remark column |

### 2.2 Receipt Detail grid

Our form shows the lines in the invoice grid (Invoice No / Invoice Amount / Pending Amount / Amount / TDS / Total); our view shows them in the invoice table (Invoice No / Amount / Pending / Received / TDS / Total Received).

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 12 | Payment of = SALES | `npaymentis` 52, `ntrnno` → `trhsales` | `paymentdetails.modulecode` = migrated `sal_order.code` | Invoice No | Invoice No | branch + invoice no |
| 13 | Payment of = AMC | `npaymentis` 53, `ntrnno` → `trdcontr3bills` | `modulecode` = the bill's invoice (`billingcycle.salcode`) | Invoice No | Invoice No (= eBizWiz bill no) | bill → `billingcycle` (`BILL_<right-10 contract no>_<n>`) → its invoice (§2.3, §7) |
| 14 | Payment of = CALLS | `npaymentis` 54, `ntrnno` → `trhcalls` | `modulecode` = the call's bill invoice | Invoice No | Invoice No (= call bill no, e.g. `BS510`) | found by label "Service Call Bill" + branch + bill no |
| 15 | Bill No | `nbillno` | — (number of the linked invoice) | Invoice No | Invoice No | equals the bill / call bill number on every line |
| 16 | Mode Amount | `nchequeamount` | `amountwithouttds`; `amount` = money + deductions | Amount · Total | Received · Total Received | app formula `amount = amountwithouttds + tds` |
| 17 | TDS / WCT / Other Deduction | `ntdsdedt`, `nwctdedt`, `nothdedt` | `tds` (sum of the three) | TDS | TDS | our form has one deduction column |
| 18 | Mode No | `vchequeno` | `payments.instno` | Instrument No | Instrument No | cheque grouping key (no + date + mode) |
| 19 | Mode Date | `dchequedt` | `payments.instdate` | Instrument Date | Instrument Date | |
| 20 | Drawn on | `nbankdrawnon` → `mstbanks` | `payments.instbank` (name) | Instrument Bank | Instrument Bank | |
| 21 | Payment Mode | `npaymode` → `mstfixedselection` | `payments.mode` | Mode | Mode | CASH→`Csh`, CHEQUE→`Chq`, DEMAND DRAFT→`DD`, NEFT / RTGS Online→`neft` |
| 22 | line remarks | `vremarks` | `payments.remark` | Notes | — | appended |

### 2.3 The bills the receipts pay (Sales Invoices, so the money has an invoice to go to)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 23 | Contract bill (Bill No, date, amount) | `trdcontr3bills` with `nbillno` | `sal_order` (`type 'SO'`), `salorderno` = bill no, `salorderdt` = bill date; `billingcycle.salcode` = it | Sales Invoice | Sales Invoice; AMC Contract → Billing Cycle "Billed" + Invoice tab | customer / contact / executive / Bill To / branch from the migrated contract; label **AMC Contract Bill** |
| 24 | (bill value) | `nbillamount`, contract `nitemtotal`, `trdcontr4posttaxchgs` | `sal_order_det` "Amc Product" qty 1 · `sal_tax` · `sal_adjust` | lines / tax | lines / tax | taxable = contract header item total × bill ÷ contract total; charges the same share; GST by the Sales Invoice rules, checked against the contract total (§7 GST rule); any rest → "Bill rounding (eBizWiz)" (≤ ₹1) or "Bill difference (eBizWiz)"; **invoice total = bill amount** |
| 25 | Service Entry bill (Bill No., date) | `trhcalls.vinvoiceprefix` + `ninvoiceno`, `dinvoicedt` | `sal_order`, `salorderno` = e.g. `BS510` | Sales Invoice | Sales Invoice | customer / contact / Bill To / engineer (executive) from the migrated complaint; remark "Service Call: 26E30007 \| Trn No: CL16505"; label **Service Call Bill** |
| 26 | (call bill lines) | `trdcalls3parts` (item, qty, rate, tax set, tax amt, remarks), `trdcalls6posttaxchgs` | `sal_order_det` per part · `sal_tax` · `sal_adjust` | lines / tax | lines / tax | same tax rules as Sales Invoice; a part whose item is not migrated → "Other items (eBizWiz)"; invoice total = `ntotalamount` |

---

## 3. Masters seeded

| Master | Rows | Source |
|---|---|---|
| `companybank` | 2 (BOB default, SBI) | `mstbanks` (bank, account no., branch, account type, `bourbank` → default) |
| `product` "Amc Product" (`type 's'`) | 1 | the app's own AMC invoice product (`gst/sales/app/sales.asp` `GetSalesAmcProduct`) |
| `label` category `SAL` | "AMC Contract Bill", "Service Call Bill" | so the two kinds of bill invoice are visible in the Sales Invoice list / Labels filter |

---

## 4. Changes made on our side

### 4a. Form / view changes (saksham-gated)

None — no form or label change for Receipt Entry; every field uses a label saksham already sees.

**New fields: 0.**

### 4b. Database changes (ALTER)

None.

---

## 5. Not migrated

| Client UI | Client DB | Rows | Why |
|---|---|---|---|
| Receipt with no line | `trhpayrg` | 409 | no amount and no document in eBizWiz either |
| Terms & Cond. | `nterms`, `vtermsconditions` | 0 | empty |
| Line without "Payment of" | `trdpayrg1details` | 1 | no amount and no document; stays on account (amount 0) |
| AMC bills not billed | `trdcontr3bills` without `nbillno` | 3,618 | schedule only — stay "Unbilled" in the Billing Cycle tab |
| AMC bills with amount 0 | `trdcontr3bills` | 4 | nothing billed; no receipt on them |
| Call bills with amount 0 | `trhcalls` with `ninvoiceno`, `ntotalamount = 0` | 23 | nothing billed; no receipt on them |
| — (our instrument "Branch") | — | — | eBizWiz has no such field; our form marks Branch required, which only matters when a user edits a migrated payment |
| Tally columns | — | — | our-side integration |

---

## 6. Verification (latest run)

| Check | Client | Ours |
|---|---|---|
| Receipts | 3,250 headers, 409 without a line | 2,841 receipts → **2,880** `payments` (one per cheque); `companybank` 2 |
| Amount | ₹18,10,20,531.23 | **₹18,10,20,531.23**; header = Σ lines on every payment |
| Receipt lines | 3,565: SALES 697 = ₹10,40,89,500.32 · AMC 2,429 = ₹6,76,60,969.02 · CALLS 438 = ₹92,70,061.89 · 1 untyped (no amount) | **3,565** `paymentdetails` = Sales Invoice 697 + AMC bill invoice 2,429 + call invoice 438 + on account 1 (amount 0) |
| Lines → bill | every AMC line points at a bill with a bill number (line `nbillno` = bill `nbillno`); every CALLS line at a billed call (438 lines, 430 calls) | every line on the matching invoice |
| AMC bills billed | 4,000 (unique per office), ₹11,70,73,427.85; 4 at amount 0 | **3,996** invoices, total **₹11,70,73,427.85**; `billingcycle.salcode` set on 3,996 |
| Call bills | 983, ₹2,41,96,132.26; 23 at amount 0 | **960** invoices, total **₹2,41,96,132.26**; 1,030 lines (4 part lines without a migrated item → "Other items (eBizWiz)") |
| AMC bills with a receipt | 2,389: 2,341 paid exactly, 2 over-paid in eBizWiz itself | 2,389: 2,341 paid exactly, 2 over-paid (the same 2), rest partly paid |
| Call bills with a receipt | 430, 0 over-paid | 430: 405 paid exactly, 0 over-paid |
| Sales Invoices with a receipt | — | 677: 644 fully paid, 33 partly, 0 over-allocated |
| Customer Advance from migrated receipts | — | **₹0** (1 on-account row, amount 0); `payments.module` = `Sal` on 2,879 of 2,880 |
| AMC bill GST (§7) | — | GST row not charged: 3 bills (MC360); GST included in the eBizWiz total: 108 bills (76 contracts); "Bill difference (eBizWiz)": 14 bills of 7 contracts (MC23, MC111, MC178, MC287, MC348, MC420, MC421) |
| Bill To for tax | — | every migrated contract and complaint has `contactbranch` |

**Record check PA1525** — FDC Ltd., Jogeshwari (W), 30/04/2024, NEFT `AXISP00495011715`, ₹53,360 + TDS 920 → invoice **2138** (MC1726 `BILL_0000MC1726_1`, 01/04/2024), invoice total 54,280 = allocated 54,280 → pending 0.

**Record check PA460** — 11/10/2019, Biocon India Ltd., cheque `182449` dt 26/09/2019, BOB, ₹20,626 → invoice SA1096.

---

## 7. Notes

**Every receipt is allocated to an invoice.** In our app an on-account `paymentdetails` row (`modulecode` NULL, `paid = 1`) is the customer's **Advance**: `gst/sales/app/sales.asp` sums those rows for the customer, `sales.js` shows "Advance: ₹…" with a **Select Payments** button on every Sales Invoice of that customer with a balance, and `app/testmodal.asp` `AdvanceListmodal` lists them. AMC and CALLS receipts left on account would be adjustable against unrelated sales invoices and the AMC contract would show "No Invoice". So AMC bills and call bills are billed as Sales Invoices and each receipt line is allocated to its invoice.

**The standard way (read in the code).** A contract billing cycle is billed by a Sales Invoice linked with `billingcycle.salcode` (`updateBillingCycle`, `LinkBillingCycleWithInvoice`); the Billing Cycle tab shows Billed / Unbilled from it, the contract Invoice tab lists `sal_order where code in (select salcode from billingcycle …)`, and `getOutStandingAmount` takes tax, adjustments and `payments.module = 'sal'` receipts from those invoices. The app's AMC invoice line is "Amc Product" (qty 1) and it never sets `sal_order.module`. A payment reaches an invoice through `paymentdetails.modulecode` + `payments.module = 'Sal'` (`addpaymentinout`). Our complaint has no invoice link, so a call bill is an ordinary Sales Invoice that names the call.

**eBizWiz AMC bills hold no tax breakup** — only bill no, date, gross amount. Example MC2089: item total 28,000, GST 18 %, total 33,040, two bills of 16,520 → each invoice: Amc Product 14,000 + GST 2,520 = 16,520. Receipt against bill 2645 of the client screen: 16,240 + TDS 280 = 16,520.

**GST rule of the AMC bill invoices (`GstByContractTotal`).** Item value = the contract header `nitemtotal`. The GST is decided by the eBizWiz contract total: a GST row whose amount is not in the total is dropped (MC360); no GST row but total = (items + other charges) × 1.18 / 1.12 / 1.05 / 1.28 → GST at that rate is added as "GST @18% (included in eBizWiz total)" (76 contracts, all 18 %, e.g. MC1057 165,000 → 194,700); anything else as entered. Contracts whose own figures disagree (e.g. MC23 items 3,600, total 55,870) keep a "Bill difference (eBizWiz)" line, because the invoice total must stay the bill amount the receipts paid.

**Label rows with an amount** (`TOTAL :` 9,440 on call bill BS2 / 17I07001; 40 rows in all charge tables) are part of the eBizWiz total (items 8,000 + GST 1,440 + "TOTAL :" 9,440 = bill 18,880), so they stay as an adjustment, the same as on migrated Sales Invoices (SA1579).

**Stock.** "Amc Product" and the LABOUR items are service products (no stock). The 2 SPARES lines on billed calls already left stock through the call's MRO (module 11), so no SAL stock row is posted for them; module 8 (Stock) runs before modules 10/11 and does not see these invoices.

**Other screens.** The Sales Invoice list also holds the AMC bill and call bill invoices (filter by the two labels). The Contract Payment Status report (`app/reports/amccontract.asp`) reads `amc.salcode` (one invoice per contract) and shows "No Invoice" — `amc.salcode` stays empty because editing a contract deletes the invoice held there (11 md).

**Run order.** Module 12 clears `payments` / `paymentdetails` itself, but the invoices are created by modules 10 and 11, which insert into a fresh DB: restore the DB to the point before module 10 and run 10 → 13 (or Run ALL).

**Field lengths.** `payments.remark` is `nvarchar(2000)` = 1,000 characters; the exe caps by characters (`remark` 1000, `instno` / `instbank` / `mode` 50, `paymentno` 250, `companybank` text 50).

**Decisions.** Seed the 2 banks · `paid = 1`, `bankricodate` = receipt date · one payment per cheque · AMC and call bills billed as Sales Invoices and every receipt line allocated to its invoice · no form change for Receipt Entry.

**View limits.** The payment view shows neither Payment Number (it is in the list) nor Notes (visible on edit); the receipt / bill references live in Notes.

_Read from `saksham70v1_1`, `SakshamRMtP15329`, `Program.cs` (`Payment`, `AmcBillInvoice`, `CallBillInvoice`, `SalesInvoice`), `payment/default.asp`, `app/payment.asp`, `app/testmodal.asp`, `gst/sales/app/sales.asp`, `gst/sales/script/sales.js`, `amc/app/contract.aspx.cs`, `app/reports/amccontract.asp`._
