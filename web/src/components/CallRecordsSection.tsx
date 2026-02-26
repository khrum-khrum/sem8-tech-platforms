import type { CallRecordDetail, PagedCallRecords } from '../api'
import { formatMoney } from '../format'

interface CallRecordsSectionProps {
  callRecords: PagedCallRecords | null
  busy: boolean
  phoneFilter: string
  onPhoneFilterChange: (value: string) => void
  onApplyFilter: () => Promise<void>
}

export function CallRecordsSection({
  callRecords,
  busy,
  phoneFilter,
  onPhoneFilterChange,
  onApplyFilter,
}: CallRecordsSectionProps) {
  if (!callRecords) {
    return null
  }

  return (
    <section className="panel">
      <h2>Детализация звонков</h2>
      <div className="filter-row">
        <input
          type="text"
          placeholder="Фильтр по номеру"
          value={phoneFilter}
          onChange={(event) => onPhoneFilterChange(event.target.value)}
        />
        <button onClick={() => void onApplyFilter()} disabled={busy}>
          Применить
        </button>
      </div>

      <p className="hint">
        Найдено записей: {callRecords.totalCount}. Страница {callRecords.page} из {callRecords.totalPages || 1}.
      </p>

      <div className="cards">
        {callRecords.items.map((call: CallRecordDetail) => (
          <article className="call-card" key={call.id}>
            <p>
              <strong>{call.callId}</strong> | {call.direction} | {call.disposition}
            </p>
            <p>
              {call.callingParty} → {call.calledParty}
            </p>
            <p>
              Длительность: {call.durationSec}s, тарифицируемо: {call.billableSec}s
            </p>
            <p>Стоимость: {formatMoney(call.computedCharge)}</p>
            <p>
              Тариф:{' '}
              {call.appliedTariff
                ? `${call.appliedTariff.id} (${call.appliedTariff.prefix}, ${call.appliedTariff.destination})`
                : 'не применён'}
            </p>
          </article>
        ))}
      </div>
    </section>
  )
}
