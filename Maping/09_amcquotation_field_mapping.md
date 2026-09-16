# AMC Quotation — Migration Mapping (eBizWiz → EdifyBiz)

Client menu **Sales → Contracts → AMC Quotation** (`trhoffer`, number prefix `QA`). Read as: what the client has → where it sits in the client DB → which column it goes to in our DB.
Rewritten 2026-09-15 (replaces the parked draft): uses the rules proven in modules 04/05/07 (tax charges, net rate) and a fresh study of the EdifyBiz AMC module. **Status: confirmed by user 2026-09-15 — exe module 9 "AMC Quotation" built + 1 form change done (see Build status at the end).**

Source (offices 2/3/4/6): `trhoffer` (8,758 headers), `trdoffer1items` (10,114 product lines), `trdoffer2itemsdet` (13,535 serial rows — rate, period, PM visits), `trdoffer3posttaxchgs` (10,525 charges on 7,486 quotations; 7,853 GST rows), `trdoffer6checks` (25,833 check items). `trdoffer4tech` 0, `trdoffer5tempbills` 1.

---

## 0) Where it goes — standard EdifyBiz (verified in code)

- EdifyBiz has **no AMC Quotation screen/table** (searched "AMC Quotation", "amcquotation", "amc quote", "proposal", "renewal" — not found). AMC menu = AMC Contract (`amc` / `contractdetails`) + AMC Complaint (`contractcall`) only; Renew copies a contract directly.
- The **standard link from a quotation to an AMC contract** is `amc.module = 'CQ'` + `amc.modulecode = inqcs.code` (`amc/app/contract.aspx.cs` joins `inqcs i on i.code=a.modulecode and a.module='CQ'`, contract view shows the quotation no. `inqref`).
- The standard "**Quotation For**" field on the quotation = `inqcs.modulename` (option "AMC Contract"; also used as `modulename='AMC Complaint'` for complaint quotes).

➡ AMC Quotation = **Customer Quotation** (`inqcs cors='CQ'` + `inqcsdet`), marked `modulename='AMC Contract'`. Next module (AMC Contract) then sets `amc.module='CQ'`, `modulecode` = this quotation (client link `trhcontr.namcquoteno`: 4,165 of 4,528 contracts).

---

## 1) Header → `inqcs` (`cors='CQ'`)

| Client (eBizWiz) | Client DB (`trhoffer`) | Our DB (`inqcs`) | Note |
|---|---|---|---|
| Trn. No. | `vtrnprefix` + `ntrnno` | `inqref` | `QA…` (sales quotations are `QT…` — no clash) |
| Trn. Date | `dtrndate` | `inqdate` | |
| (office) | `nofficeid` → branch 3/4/5/6 | `branchcode` | |
| Party | `nparty` → migrated `contact` | `cscode` | 8,758 filled |
| (customer branch) | default `mltaddress` of the party | `csbranch` | same as 04 |
| Party Contact | `npartycontact` → `mltcontact` | `cperson` | |
| Sales Person | `nsalesman` → `users` | `executive` | fallback Admin |
| Offer Type | `noffertype` (FROM CONTRACT 4,669 · FROM WARRANTY 3,103 · FROM CALLS 962 · FRESH 21) | `quotetype` → `miscellaneous(Inquiry/Quote Type)` "AMC - FROM CONTRACT" … | D2 |
| (AMC marker) | — | `modulename = 'AMC Contract'` | D2 |
| Follow-up Date | `dfollowupdate` | `followupdate` | 652 filled |
| Ref No / Ref Date | `vrefno` / `drefdate` | `enqrefno` / `enqrefdate` | |
| Terms & Conditions | `vtermsconditions` | `terms` | |
| Remarks + Comment | `vremarks`, `vcomment` | `remark` | |
| Payment Schedule + Begin/End | `npaymentschedule` → `mstpaymentschedule` (Advance 4,212 · Half Yearly 2,006 · Yearly 1,444 · Quarterly 1,012), `vbegorend` | `remark` (labelled) | no payment-schedule column on `inqcs` |
| Conversion chance | `nconversion` (YES 271 · MAY BE 179 · DONT KNOW 330 · NO 10) | `remark` (labelled) | |
| Approved | `bapproval` = 1 on all 8,758 | `approvalstatus` | D4 |
| Status | — (no status in source) | `status` | D4 |
| — | — | `currency` = INR, `comcode` | no currency column in `trhoffer` |

Not migrated: `ncheckset` (replaced by the check list, D5), `vletterhead` (print), `nterms` (code only), header `ndiscount` (= Σ(master rate − rate) of the serial rows on 1,303 of 1,711 — information only, never deducted), `nitemtotal`/`ntotalamount` (derived).

---

## 2) Lines → `inqcsdet` — one line per equipment serial

Rate, discount, period and PM visits are on the **serial row**; a product line has 1 serial on 8,260 lines and 2–19 serials on 1,848 (same rate on 1,622 of them, but periods/serials differ). One serial = one quotation line — also 1:1 with the AMC contract line (`contractdetails` is per serial).

| Client | Client DB | Our DB (`inqcsdet`) |
|---|---|---|
| Item | `trdoffer1items.nitem` → migrated `product` | `pcode` |
| — | — | `quantity` = 1 |
| Master Rate / Discount % / Rate | `trdoffer2itemsdet.nmasterrate`, `ndiscountperc`, `nrate` (net: `nrate` = master × (1 − %) on 233 of 233 discounted rows) | discounted → `price` = master, `discount` = master − rate (per unit), `discountpercent` = %; else `price` = `nrate` (same rule as 04/05) |
| Serial No. | `vserialno` | `prodsrno` (nvarchar 50) + in `pdesc` |
| Location | `vlocation` | `location` (nvarchar 255) + in `pdesc` |
| GST % | from the quotation's GST charge (§3); none → line Tax Set | `prodtax` |
| Contract type / period / PM visits / old end / sign-up or renewal / previous document | `ncontrtype` (NON-COMPREHENSIVE 10,736 · COMPREHENSIVE 2,797 · SEMI 2), `dstartdate`, `denddate`, `nmonths`, `npmvisits`, `doldenddate`, `trdoffer1items.nofferis` (CONTRACT RENEWAL 5,497 · CONTRACT SIGNUP 4,614), `nprevnumber` (renewal → old contract 5,489; sign-up → sales invoice 3,670) | `pdesc` (Product Description, standard) as labelled text: `S/N … · Location … · NON-COMPREHENSIVE (TYPE 2) · 01/04/2025 – 31/03/2026 (12 m) · PM visits 2 · Old end 31/03/2025 · CONTRACT RENEWAL · Prev: MC… / SA… · Contract: MC… (or Closed)` |

Serial check: 9,606 of 13,535 serial numbers exist on migrated Sales Invoices (serial text), 9,362 on eBizWiz AMC contracts. `nitemserialno` points to a serial master that is not in the source DB → not usable. Not carried: `nmanufacturer`, `dfirstinstdate`, `bclosed`, per-serial `bapproved`/`dapprovedate`/`nnewcontractno` (the contract link is made from the contract side).

---

## 3) Tax & charges → `inq_adjust` (same as 04/05)

- GST is a post-tax charge: "Add: GST as applicable" (PERCENTAGE, 18 % stored on 3,449 rows), "Add : IGST @18%", "CGST @9%" + "SGST @9%", "GST @18%", "Less : Discount" (AMOUNT 253), "Tax = Nil, SEZ" 147, old "Service Tax" 99 …
- **Formula verified:** item total = Σ serial `nrate`; % charge = item total × %; AMOUNT as entered; total = items + line tax + charges → **8,696 of 8,714** quotations with a source total.
- Every non-zero charge → `inq_adjust` (name, %, amount) in entry order; line Tax Set amounts → "Tax on items (eBizWiz)"; unmapped product → "Other items (eBizWiz)". GST % → `inqcsdet.prodtax` (NIL / > 28 % / no amount = no GST). The saksham quotation view shows no tax column and adds `inq_adjust` → Grand Total = eBizWiz total.

---

## 4) Check list → `taskchecklist`

`trdoffer6checks` (+ `mstchecks`): 25,833 items on 4,263 quotations (9,510 done) → `taskchecklist` (title, status, due date, assign to, remark), `modulecode` = quotation `inqcs.code`, own tag (D5). Same mapping as the Sales Order check list (05 §5).

---

## 5) Final recommendation after re-check (for your confirmation)

Re-checked in code/DB 2026-09-15 — only standard fields, no new masters/columns, nothing guessed:

| # | Decision | Why (verified) | Needs |
|---|---|---|---|
| D1 | Customer Quotation (`inqcs` CQ), one line per serial | only quotation the standard AMC Contract links to (`amc.module='CQ'`); contract lines are per serial | exe |
| D2 | `quotetype` = "AMC - FROM CONTRACT / FROM WARRANTY / FROM CALLS / FRESH" (added to the existing Quote Type master — visible + filterable for saksham) and `modulename = 'AMC Contract'` (standard data value; the "Quotation For" field is Zinq-only in the view, so it stays hidden — **no form change**) | Quote Type dropdown already shown for saksham (module 04); `modulename` only drives the Zinq/Skytech view block and `getModuleReference` | exe |
| D2b | Serial No. and Location also written into `pdesc` | saksham quotation view shows only product name + `pdesc` (`inquiry.js` ~6947); `prodsrno` / `location` columns are not shown for saksham (location = aromatherapy only) — still filled | exe |
| D3 | Tax & charges → `prodtax` + `inq_adjust` | same as 04/05 | exe |
| D4 | `approvalstatus = 'Approved'` (all 8,758 approved in eBizWiz). **Status from the eBizWiz serial rows** (`nnewcontractno` = contract created, `bclosed` = closed), new values seeded in the Quotation status master (`status` module='Quotation'): **AMC - Contract Signed** (all serials converted, 3,999) · **AMC - Partly Contract Signed** (some converted, 126) · **AMC - Closed** (closed, no contract, 4,359 + 3 partly closed) · **AMC - Open** (221 + 50 with no serial row). Per serial the contract no. / "Closed" is also written in `pdesc`. | eBizWiz keeps the outcome on each serial (approved + new contract no., closed); no status column on the header. User rule: keep the data, adding status values is fine | exe |
| D5 | Check list 25,833 items → `taskchecklist` `module='CQ'`, `modulecode = inqcs.code` | standard `TaskCheckListFill` component already used for SO | exe + **1 form change** |
| D6 | Previous document / conversion chance / payment schedule → text (`pdesc` / `remark`) | no standard column; contract link made from AMC Contract module | exe |

### Form change (only one, saksham-gated) — `assets/scripts/inquiry.js` (view fill, ~line 5150)
Today the Task Check List tab is shown only on Sales Order:
```js
if (aInquiry[16] == 'SO') {
    ...handleCheckListViewFillModule($('#taskchecklist'), 'SO', aCurInquiry.inquiry[0], "");
```
Change: show it for Quotations too, with the document's own tag (`'SO'` or `'CQ'`):
```js
if (aInquiry[16] == 'SO' || aInquiry[16] == 'CQ') {
    ...handleCheckListViewFillModule($('#taskchecklist'), aInquiry[16], aCurInquiry.inquiry[0], "");
```
Effect: every saksham quotation (AMC and sales) gets the standard Task Check List tab; existing SO checklists unchanged. No other form / ASP / DB column change.

---

## Build status (2026-09-15, approved "go ahead")

- **exe:** `AmcQuotation` class in `Program.cs`, menu **9. AMC Quotation** (runs after Stock in Run ALL; needs Contact + Product + users). Seeds `miscellaneous(Inquiry/Quote Type)` "AMC - FRESH / FROM WARRANTY / FROM CONTRACT / FROM CALLS" (full `noffertype` group) and `status(Quotation)` "AMC - Contract Signed / Partly Contract Signed / Closed / Open". Header `inqcs` CQ with `modulename='AMC Contract'`, `approvalstatus='Approved'` (the value the app's Approve action writes, `app/inquiry.asp:4111`); one `inqcsdet` per serial (net-rate rule, `prodtax`, `prodsrno`, `location`, `pdesc` HTML lines with `<br/>`); product lines without serial rows keep qty and offer type text; charges via `Shared.ComputeCharges` → `inq_adjust`; check list → `taskchecklist module='CQ'`.
- **form (saksham only):** `assets/scripts/inquiry.js` view fill — Task Check List tab condition `aInquiry[16] == 'SO' || aInquiry[16] == 'CQ'`, tag `aInquiry[16]`. Verified the tab markup (`inquiry/default.asp` `li.showso` + `#tabTaskchecklist`) is rendered for saksham and every `.showso` hide in `inquiry.js` is Akpowertech-only.
- Project code checked for each field (not only DB columns): `txtquotetype` / `txtstatus` / `txtcperson` store codes, `txtterms` text, `txtpdesc` CKEditor HTML, `txtmodulename` value "AMC Contract", `GetInquiryProductArray` reads `prodsrno` and `location`.

_Form re-check 2026-09-15: remark now also carries "Terms & Cond.: <msttermset name>" (`nterms`, 1,436 = ESCO Standard Terms and Conditions), "Check List: <mstcheckset name>" (`ncheckset`, 6,845 — the template; the actual items are in `trdoffer6checks` → Task Check List) and the payment schedule with "Beginning of Period / End of Period" instead of B / E._
