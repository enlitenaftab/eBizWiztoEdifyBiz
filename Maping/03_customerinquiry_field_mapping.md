# Customer Inquiry — Migration Mapping (eBizWiz → EdifyBiz)

Client module **Inquiry Entry**. Read as: what the client sees → where it sits in the client DB → which column it goes to in our DB → how it shows in our UI.

The action / follow-up part (1st Action, Action Taken, Next Action) follows the model the client (Tejas) and senior confirmed on call: **Purpose + Action Taken are one record; Next Action is a separate follow-up; person = contact code (not name); sales person = the follow-up creator.**

Source tables: `trhinqry` (header), `trdinqry1prods` (line items), `trdinqry2actions` (DSR actions / follow-ups).
Target tables: `inqcs` (cors='CI', header), `inqcsdet` (line items), `followup` (module='INQ', actions).
Scope: offices 2 (HO Mumbai), 3 (Bangalore), 4 (Delhi), 6 (Kolkata). Test offices 1 & 5 excluded.

---

## 1) Inquiry header → `inqcs` (cors='CI')

| Client UI (eBizWiz) | Client DB (`trhinqry`) | Our DB (target column) | Our UI (EdifyBiz) |
|---|---|---|---|
| Trn. No. | `vtrnprefix` + `ntrnno` | `inqcs.inqref` | Inquiry No. (auto) |
| Trn. Date | `dtrndate` | `inqcs.inqdate` | Inquiry Date |
| Party | `nparty` → target `contact` (by name) | `inqcs.cscode` | Customer |
| (branch of that customer) | party is per-branch (no field) | `inqcs.csbranch` | Customer Branch (customer's own address) |
| Contact Person | `npartycontact` → `mltcontact.code` | `inqcs.cperson` | Contact Person |
| Sales Person | `nsalesman` → target `users` (by name; all source users migrated in Contact) | `inqcs.executive` | Executive (real salesperson; fallback Admin if unmatched) |
| Inquiry By | `ninquiryby` → `miscellaneous(Inquiry/Call Type)` | `inqcs.calltype` | Inquiry By |
| Category | `ninquirycategory` → `miscellaneous(Inquiry/Category)` | `inqcs.Category` | Category |
| Source | `ninquirysource` → `miscellaneous(Inquiry/Inquiry Source)` | `inqcs.sources` (name) | Source |
| Inquiry Details | `vinquirydetails` | `inqcs.remark` | Remarks |

---

## 2) 1st Action / Action Taken / Next Action → `followup` (DSR model)

The **Type** stays on the inquiry header; each action row becomes a `followup` (module='INQ', modulecode = inquiry code).

| Client UI (eBizWiz) | Client DB | Our DB (target column) | Our UI (EdifyBiz) |
|---|---|---|---|
| Type (DSR - No Action / DSR - Action Needed / Sales Lead) | `trhinqry.nactionType` → `mstfixedselection` | `inqcs.types` | Type (drives show/hide of the sections below) |
| Purpose | `trdinqry2actions.nactiontobetaken` → `mstactiontobetaken` | `followup.purpose` | Purpose |
| Action Taken | `nactiontaken` → `mstactiontaken` | `followup.remarkscode` (→ `followupremarks`) + `followType` | Action Taken |
| Priority | `npriority` → `priority` master | `followup.priority` | Priority |
| Date (planned) | `dactiontobedate` | `followup.nextfollowup` | Date |
| Action Taken Date | `dactiondate` | `followup.lastfollowup` | Action Taken Date |
| Purpose Remarks | `vactiontoberemarks` | `followup.remarks` (prefixed "Purpose Remark: ") | Remarks (single box) |
| Action Taken Remarks | `vremarks` | `followup.remarks` | Remarks (single box) |
| Person Contacted | `npartycontact1` → `mltcontact.code` | `followup.person` = `pcode` | Person Contacted |
| Sales Person | `nsalesman` | `followup.createdby` | (the follow-up's owner / assignee) |
| Next Action To Be Taken (only for DSR - Action Needed) | separate `trdinqry2actions` row | a **separate** `followup` row (future `nextfollowup`, assigned to Sales Person) | Next Action To Be Taken |

Notes:
- Type is a fixed 3-value dropdown; it decides which sections show (No Action / Action Needed / Sales Lead) — per the client's DSR flow.
- The follow-up's `followType` (Call / E-Mail / Meeting / Fax / SMS / Others) is derived from the action text by keyword.

---

## 3) Inquiry Progress Details → `inqcs`

| Client UI (eBizWiz) | Client DB (`trhinqry`) | Our DB (target column) | Our UI (EdifyBiz) |
|---|---|---|---|
| Sales Stage | `nsalesstage` → `miscellaneous(Inquiry/Sales Stage)` | `inqcs.stage` | Sales Stage |
| Status | `ninquirystatus` → `status` master | `inqcs.status` | Status |
| Lost Reason | `nlostreasons` → `miscellaneous(Inquiry/Loss Reason)` | `inqcs.lossreason` | Lost Reason |
| Approx. Close Date | `dclosingdate` | `inqcs.validdate` | Approx. Close Date |
| Remarks | `vremarks` | `inqcs.remark` | Remarks |

---

## 4) Line items → `inqcsdet`

| Client UI | Client DB (`trdinqry1prods`) | Our DB (`inqcsdet`) | Our UI |
|---|---|---|---|
| Product | `nitem` → target `product` (by name) | `pcode` | product row |
| Quantity | `nquantity` | `quantity` | quantity |

---

## 5) Fields folded into Remarks (no structured target)

Appended (labelled) into `inqcs.remark`, nothing lost:

| Client UI (eBizWiz) | Client DB (`trhinqry`) |
|---|---|
| Comment | `vcomment` |
| Probability % of Getting The Order | `nprobability` |
| Inquiry Value | `ninqvalue` |
| Campaign | `ntrhcampa` |

---

## 6) Not migrated / auto (client field with no source data to carry)

| Client UI | Reason |
|---|---|
| Check List | process master, not migrated |
| Quote No. / Quote Value | auto — filled by the linked Quotation, not entered here |
| Order No. / Order Value | auto — filled by the linked Order, not entered here |

_Tax (checked 2026-09-15): eBizWiz inquiry lines (`trdinqry1prods`) hold only the product — no rate, discount or tax, and inquiries have no post-tax charges. Nothing tax-related to migrate for CI (GST starts at the Quotation, 04)._
