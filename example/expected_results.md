# Expected Results for Example Files

Date of all calls: **2026-02-24 (Tuesday = weekday 2, falls within Mon–Fri range 1-5)**

---

## Tariff Table

| # | Prefix | Rate/min | Conn. Fee | Timeband     | Weekday | Priority |
|---|--------|----------|-----------|--------------|---------|----------|
| 1 | 7916   | 1.80     | 0.00      | 08:00–20:00  | 1-5     | 100      |
| 2 | 7916   | 2.50     | 0.00      | (all day)    | 6,7     | 90       |
| 3 | 791    | 3.00     | 0.50      | (all day)    | 1-7     | 50       |
| 4 | 7495   | 0.90     | 0.10      | (all day)    | 1-7     | 100      |

---

## Call-by-Call Analysis

### Call A — `call0001`
- Direction: **outgoing** → lookup number = CalledParty = `79161234567`
- Disposition: `answered` → **billed**
- Normalized: `79161234567`
- Prefix matches: `7916` (tariff #1, #2), `791` (tariff #3)
- Tariff #1 check: effective ✓, timeband 08:00–20:00 (call at 10:00) ✓, weekday Tue(2) in 1-5 ✓ → **candidate, prefix len=4, priority=100**
- Tariff #3 check: effective ✓, timeband all ✓, weekday Tue(2) in 1-7 ✓ → candidate, prefix len=3, priority=50
- Winner: tariff #1 (longer prefix wins: 4 > 3)
- **Charge = 0.00 + (120 / 60) × 1.80 = 0.00 + 2.00 × 1.80 = 3.6000**
- Subscriber: `78123260000` (Иванов Иван Иванович)

### Call B — `call0002`
- Direction: **incoming** → lookup number = CallingParty = `74951112233`
- Disposition: `answered` → **billed**
- Normalized: `74951112233`
- Prefix matches: `7495` (tariff #4)
- Tariff #4 check: effective ✓, timeband all ✓, weekday Tue(2) in 1-7 ✓ → **candidate**
- Winner: tariff #4 (only match)
- **Charge = 0.10 + (60 / 60) × 0.90 = 0.10 + 1.00 × 0.90 = 1.0000**
- Subscriber: `78123261111` (Петров Пётр Петрович) — CalledParty is sub2's number

### Call C — `call0003`
- Direction: **outgoing** → lookup number = CalledParty = `79162345678`
- Disposition: `answered` → **billed**
- Normalized: `79162345678`
- Prefix matches: `7916` (tariff #1, #2), `791` (tariff #3)
- Tariff #1 check: timeband 10:00 ✓, weekday 2 ✓ → **candidate, prefix len=4, priority=100**
- Winner: tariff #1
- **Charge = 0.00 + (90 / 60) × 1.80 = 0.00 + 1.5 × 1.80 = 2.7000**
- Subscriber: `78123260000` (Иванов Иван Иванович)

### Call D — `call0004`
- Disposition: `busy` → **NOT billed** (skipped)

### Call E — `call0005`
- Direction: `internal` → **NOT billed** (always skipped)

### Call F — `call0006`
- Direction: **outgoing** → lookup number = CalledParty = `79189999999`
- Disposition: `answered` → **billed**
- Normalized: `79189999999`
- Prefix matches: `791` (tariff #3) — note: `7916` does NOT match `7918...`
- Tariff #3 check: effective ✓, timeband all ✓, weekday 2 in 1-7 ✓ → **candidate**
- Winner: tariff #3 (only match)
- **Charge = 0.50 + (45 / 60) × 3.00 = 0.50 + 0.75 × 3.00 = 0.50 + 2.25 = 2.7500**
- Subscriber: `78123261111` (Петров Пётр Петрович)

---

## Expected `/results/summary`

| phone_number | client_name             | total_charge |
|--------------|-------------------------|--------------|
| 78123260000  | Иванов Иван Иванович    | **6.3000**   |
| 78123261111  | Петров Пётр Петрович    | **3.7500**   |

Breakdown:
- Иванов: Call A (3.6000) + Call C (2.7000) = **6.3000**
- Петров: Call B (1.0000) + Call F (2.7500) = **3.7500**

---

## Expected `/results/calls` (billed calls only)

| CallID   | CallingParty | CalledParty  | Direction | BillableSec | Tariff Prefix | Rate/min | Conn. Fee | Charge |
|----------|-------------|--------------|-----------|-------------|---------------|----------|-----------|--------|
| call0001 | 78123260000 | 79161234567  | outgoing  | 120         | 7916          | 1.80     | 0.00      | 3.6000 |
| call0002 | 74951112233 | 78123261111  | incoming  | 60          | 7495          | 0.90     | 0.10      | 1.0000 |
| call0003 | 78123260000 | 79162345678  | outgoing  | 90          | 7916          | 1.80     | 0.00      | 2.7000 |
| call0006 | 78123261111 | 79189999999  | outgoing  | 45          | 791           | 3.00     | 0.50      | 2.7500 |

---

## Charge Formula Reference

```
Charge = ConnectionFee + (BillableSec / 60) × RatePerMin
```
Rounded to 4 decimal places.

### Verification

| Call | BillableSec | Formula                             | Result  |
|------|-------------|-------------------------------------|---------|
| A    | 120         | 0.00 + (120/60) × 1.80              | 3.6000  |
| B    | 60          | 0.10 + (60/60) × 0.90              | 1.0000  |
| C    | 90          | 0.00 + (90/60) × 1.80              | 2.7000  |
| F    | 45          | 0.50 + (45/60) × 3.00              | 2.7500  |
