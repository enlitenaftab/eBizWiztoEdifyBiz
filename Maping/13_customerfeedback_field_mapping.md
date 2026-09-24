# 13. Customer Feedback / Survey → Complaint Customer Rating — field mapping

## 1. Overview

| | Client (eBizWiz) | Ours (EdifyBiz) |
|---|---|---|
| Screen / menu | Service → Customer Feedback / Survey | AMC → Complaint → view → Customer Rating block |
| Tables | `trdcalls5happy` (41,256 rows) + masters `mstquestion`, `mstanswer` | `customerfeedback` (`module = 'COM'`, `modulecode` = `contractcall.code`) |
| Code | — | view `amc/complaint/default.asp` (Customer Rating block), JS `amc/scripts/complaint.js` (saksham fill block), backend `amc/app/complaint.aspx.cs` (`GetComplaintArray`, LEFT JOIN `customerfeedback`) |
| Exe | — | module **13. Customer Feedback (Survey)**, class `CustomerFeedback` (needs module 11) |
| Scope | offices 2, 3, 4, 6 (1 and 5 are WinMax test offices) | |
| Status | | migrated and verified |

A survey is 12 answered questions attached to a call. One surveyed call = one `customerfeedback` row, because `GetComplaintArray` joins the feedback per complaint, so a second row would duplicate the complaint.

---

## 2. Field mapping

### 2.1 Survey header

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 1 | Call | `trdcalls5happy.ncalls` (+ `nofficeid`) | `customerfeedback.modulecode` | — | the complaint the block sits on | office\|call → migrated `contractcall.code` (3,420 / 3,420 found) |
| 2 | — | — | `module` | — | — | `COM` |
| 3 | Overall CSI Rating (Put exact %) — Q16 | `vremarks` of question 16 | `rating` | — | Customer Rating (1–5 stars) | §7 rating rule |
| 4 | Saved on | `addedon` of the rated survey | `ratingdate`, `createdon` | — | Customer Rated On (`createdon`) | `updatedon` = `editedon` |
| 5 | Saved by | `addedby` | `createdby` / `updatedby` | — | — | migration user |

### 2.2 Questions → `customerfeedback.remarks`

Every question becomes one line `Q: <question> - A: <answer>` in `remarks` (view: **Customer Feedback Remark**, one line each). Answer = the `mstanswer` name when one is picked, otherwise the `vremarks` text, otherwise `-` (no client row has both an answer and a text, so nothing is dropped).

| # | Client UI (exact `mstquestion.vname`) | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 6 | Q2 Was the Instrument / Equipment delivered to you on the committed time? | `nquestion = 2`, `nanswer` | `remarks` line | — | Customer Feedback Remark | answers Yes / No |
| 7 | Q3 Is the Instrument / Equipment being installed / service as per the requirement? | 3 | `remarks` line | — | Customer Feedback Remark | answers Yes / No |
| 8 | Q5 How do you rate the overall execution of your order? | 5 | `remarks` line | — | Customer Feedback Remark | answers Good / Satisfactory / Poor / Not Applicable / Pending; feeds the fallback rating (§7) |
| 9 | Q6 How do you rate the quality of Instrument / Equipment ordered? | 6 | `remarks` line | — | Customer Feedback Remark | as Q5 |
| 10 | Q7 How do you rate our response towards your requirement on Sales / Service? | 7 | `remarks` line | — | Customer Feedback Remark | as Q5 |
| 11 | Q8 How do you rate our Service Support? | 8 | `remarks` line | — | Customer Feedback Remark | as Q5 |
| 12 | Q9 How do you rate our Validation and Documentation Support? | 9 | `remarks` line | — | Customer Feedback Remark | as Q5 |
| 13 | Q10 Would you need other instruments from us, if yes.. How immediate would be your new requirement? | 10, `vremarks` | `remarks` line | — | Customer Feedback Remark | free text / Not Now |
| 14 | Q11 Anybody else you will recommend who will need these kinds of Instruments? | 11, `vremarks` | `remarks` line | — | Customer Feedback Remark | free text |
| 15 | Q12 Whether would you like to get into AMC? | 12 | `remarks` line | — | Customer Feedback Remark | answers Yes / No |
| 16 | Q13 Any suggestions to improve our Support System? | 13, `vremarks` | `remarks` line | — | Customer Feedback Remark | free text |
| 17 | Q16 Overall CSI Rating (Put exact %) | 16, `vremarks` | `remarks` line + `rating` | — | Customer Feedback Remark + Customer Rating | "100%", "95.23%", "NA"…; gives the rating (§7) |

Each question above has exactly 3,438 answers (one per survey). Q1 "HAS CUSTOMER FILLED UP THIS FORM", Q14 "CSI (Select NA for Q12…)" and Q15 "test question" have **0** answers.

---

## 3. Masters seeded

None. Our app has no survey question / answer master, so the text is written into the remark.

---

## 4. Changes made on our side

### 4a. Form / view changes (saksham-gated)

| # | Screen | Change | Kind | File | DB column |
|---|---|---|---|---|---|
| 1 | Complaint view | Existing Customer Rating / Customer Rated On / Customer Feedback Remark block opened for saksham: gate `Zinq or Skytech` + `or saksham` | Layout | `amc/complaint/default.asp` (~line 1625) | `customerfeedback.rating`, `createdon`, `remarks` |
| 2 | Complaint view | Fill of those three fields for saksham: an `if (companysname == "saksham")` block after the Zinq/Skytech one (the original fill is Zinq/Skytech-only). Stars 1 red / 2–4 orange / 5 green, "Not Found" when there is no rating; remark line breaks shown as `<br/>` | Behaviour | `amc/scripts/complaint.js` (after the Zinq/Skytech block, before `handleLogs()`) | same |

**New fields: 0** (existing fields made visible and filled; labels unchanged). The WhatsApp "Send FeedBack" button stays Zinq-only. No backend change — `GetComplaintArray` returns rating [57], rated on [58], remark [59] for every company except KHC.

### 4b. Database changes (ALTER)

None.

---

## 5. Not migrated

| Client UI | Client DB | Rows | Why |
|---|---|---|---|
| Survey rows of offices 1 and 5 | `trdcalls5happy` | 400 | WinMax test offices |
| Question / answer masters | `mstquestion` (15), `mstanswer` (12) | — | no such master on our side; the text is kept in each remark |

Every client survey row of offices 2/3/4/6 is represented.

---

## 6. Verification (latest run)

| Check | Client | Ours |
|---|---|---|
| Survey rows | 41,256 | all in remarks |
| Surveys (12 answers each) | 3,438 | 3,438 (14 calls surveyed twice, 2 calls three times: 2\|2147, 2\|14242) |
| Surveyed calls | 3,420 | **3,420** `customerfeedback` rows (`COM`) |
| Rows pointing at a missing complaint | — | 0 |
| Complaints with more than one feedback row | — | 0 |
| `ratingdate` ≠ `createdon` | — | 0 |

**Ratings:** 1: 5 · 2: 116 · 3: 493 · 4: 845 · 5: 1,948 · none: 13. That is 3,371 from the CSI %, 36 from the Q5–Q9 average, and 13 without a rating: in those, CSI is "NA" and every rating question is "Not Applicable", so "Not Found" is the true answer.

**Record check — call `25C15019`** (eBizWiz office 2, call 15043): rating 5, rated on 01/06/2026 14:48, remark with 12 lines (Yes / Yes / Good ×5 / "Yes" / "Yes" / Yes / "Not Now" / "Overall CSI Rating (Put exact %) - A: 100%") — same as the client's survey.

---

## 7. Notes

**Rating rule (D1)** — the client's own CSI bands (`mstanswer` 8–11) stretched to 5 stars: below 39% → 1, 39–66% → 2, 67–84% → 3, 85–99% → 4, 100% → 5. If Q16 holds no number ("NA", "Excellent"…), the average of Q5–Q9 is used (Good 5 / Satisfactory 3 / Poor 1, Not Applicable and Pending ignored), rounded (D1b). Nothing usable → NULL.

**Repeated surveys (D3)** — surveys are split where a question repeats in `ncode` order (the blocks are consecutive, saved about a second apart, never interleaved). All surveys of a call go into one remark under `--- Survey n ---` headers; the last one is marked `(rated)` and gives the rating and date.

**View fill** — the Zinq/Skytech fill reads its colour from `aComplaint[56]`, which is the assigned role, not the rating; the saksham block uses the rating `aComplaint[57]`.

**Idempotent** — the module first deletes `customerfeedback` rows with `module = 'COM'` (only this module writes them).

_Read from `saksham70v1_1`, `SakshamRMtP15329`, `Program.cs` (class `CustomerFeedback`), `amc/complaint/default.asp`, `amc/scripts/complaint.js`, `amc/app/complaint.aspx.cs`._
