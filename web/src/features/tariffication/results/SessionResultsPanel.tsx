import { useEffect, useMemo, useState } from 'react'
import filterIconUrl from '../../../assets/icons/filter.png'
import { getCallRecords, getSummary } from '../../../api'
import type { CallRecordDetail, PagedCallRecords, SubscriberSummary } from '../../../api'
import { formatMoney } from '../../../format'
import { Pagination } from './components/Pagination'

type TabKey = 'summary' | 'calls'

export type ResultsModel = {
  summary: SubscriberSummary[]
  setSummary: (value: SubscriberSummary[]) => void
  callRecords: PagedCallRecords | null
  setCallRecords: (value: PagedCallRecords | null) => void
  phoneFilter: string
  setPhoneFilter: (value: string) => void
}

type SummaryFilters = {
  phone: string
  name: string
}

type CallsFilters = {
  callId: string
  direction: string
  disposition: string
  callingParty: string
  calledParty: string
  tariffId: string
}

function includesInsensitive(haystack: string, needle: string) {
  const n = needle.trim().toLowerCase()
  if (!n) return true
  return haystack.toLowerCase().includes(n)
}

function uniq(values: (string | null | undefined)[]) {
  return Array.from(new Set(values.filter(Boolean) as string[])).sort((a, b) => a.localeCompare(b))
}

export function SessionResultsPanel(props: { sessionId: string; busy: boolean; model: ResultsModel }) {
  const [tab, setTab] = useState<TabKey>('summary')
  const [filtersOpen, setFiltersOpen] = useState(false)
  const [selectedTariff, setSelectedTariff] = useState<CallRecordDetail['appliedTariff'] | null>(null)
  const [summaryFilters, setSummaryFilters] = useState<SummaryFilters>({ phone: '', name: '' })
  const [callsFilters, setCallsFilters] = useState<CallsFilters>({
    callId: '',
    direction: '',
    disposition: '',
    callingParty: '',
    calledParty: '',
    tariffId: '',
  })

  const [callsPage, setCallsPage] = useState(1)
  const callsPageSize = 50

  const [summaryPage, setSummaryPage] = useState(1)
  const summaryPageSize = 20

  useEffect(() => {
    if (props.model.summary.length > 0) return
    void getSummary(props.sessionId)
      .then(props.model.setSummary)
      .catch(() => {
        // errors are surfaced via existing error panel in flow; keep silent here
      })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [props.sessionId])

  useEffect(() => {
    void getCallRecords(
      props.sessionId,
      undefined,
      callsPage,
      callsPageSize,
    )
      .then(props.model.setCallRecords)
      .catch(() => {
        // see note above
      })
  }, [callsPage, callsPageSize, props.model, props.sessionId])

  const filteredSummary = useMemo(() => {
    const phone = summaryFilters.phone.trim()
    const name = summaryFilters.name.trim()
    return props.model.summary.filter(
      (row) => includesInsensitive(row.phoneNumber, phone) && includesInsensitive(row.clientName, name),
    )
  }, [props.model.summary, summaryFilters.name, summaryFilters.phone])

  const summaryTotalPages = Math.max(1, Math.ceil(filteredSummary.length / summaryPageSize))
  const summaryPageSafe = Math.min(Math.max(1, summaryPage), summaryTotalPages)
  const summaryPageItems = filteredSummary.slice(
    (summaryPageSafe - 1) * summaryPageSize,
    summaryPageSafe * summaryPageSize,
  )

  const callItemsFiltered = useMemo(() => {
    const items = props.model.callRecords?.items ?? []
    return items.filter((c) => {
      if (!includesInsensitive(c.callId, callsFilters.callId)) return false
      if (!includesInsensitive(c.callingParty, callsFilters.callingParty)) return false
      if (!includesInsensitive(c.calledParty, callsFilters.calledParty)) return false
      if (callsFilters.direction && c.direction !== callsFilters.direction) return false
      if (callsFilters.disposition && c.disposition !== callsFilters.disposition) return false
      if (callsFilters.tariffId) {
        const tid = Number(callsFilters.tariffId)
        if (!Number.isFinite(tid)) return false
        if ((c.appliedTariff?.id ?? null) !== tid) return false
      }
      return true
    })
  }, [
    callsFilters.callId,
    callsFilters.calledParty,
    callsFilters.callingParty,
    callsFilters.direction,
    callsFilters.disposition,
    callsFilters.tariffId,
    props.model.callRecords,
  ])

  const directions = useMemo(
    () => uniq((props.model.callRecords?.items ?? []).map((c) => c.direction)),
    [props.model.callRecords],
  )
  const dispositions = useMemo(
    () => uniq((props.model.callRecords?.items ?? []).map((c) => c.disposition)),
    [props.model.callRecords],
  )
  const tariffIds = useMemo(
    () =>
      uniq((props.model.callRecords?.items ?? []).map((c) => (c.appliedTariff ? String(c.appliedTariff.id) : null))).sort(
        (a, b) => Number(a) - Number(b),
      ),
    [props.model.callRecords],
  )

  return (
    <section className="results">
      <header className="results-header">
        <div className="results-tabs" role="tablist" aria-label="Результаты тарификации">
          <button
            type="button"
            role="tab"
            aria-selected={tab === 'summary'}
            className={tab === 'summary' ? 'app-nav-button app-nav-button--active results-tab' : 'app-nav-button results-tab'}
            onClick={() => setTab('summary')}
          >
            Итоги по абонентам
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={tab === 'calls'}
            className={tab === 'calls' ? 'app-nav-button app-nav-button--active results-tab' : 'app-nav-button results-tab'}
            onClick={() => setTab('calls')}
          >
            Детализация звонков
          </button>
        </div>

        <button
          type="button"
          className={filtersOpen ? 'icon-button icon-button--small icon-button--active' : 'icon-button icon-button--small'}
          onClick={() => setFiltersOpen((v) => !v)}
          aria-label="Фильтры"
          title="Фильтры"
        >
          <img className="icon-button-img" src={filterIconUrl} width={18} height={18} alt="" aria-hidden="true" />
        </button>
      </header>

      {filtersOpen ? (
        <div className="filters">
          {tab === 'summary' ? (
            <div className="filters-grid">
              <label className="field">
                <span className="field-label">Телефон</span>
                <input
                  type="text"
                  value={summaryFilters.phone}
                  onChange={(e) => {
                    setSummaryPage(1)
                    setSummaryFilters((p) => ({ ...p, phone: e.target.value }))
                  }}
                />
              </label>
              <label className="field">
                <span className="field-label">Абонент</span>
                <input
                  type="text"
                  value={summaryFilters.name}
                  onChange={(e) => {
                    setSummaryPage(1)
                    setSummaryFilters((p) => ({ ...p, name: e.target.value }))
                  }}
                />
              </label>
            </div>
          ) : (
            <div className="filters-grid">
              <label className="field">
                <span className="field-label">Кто звонил</span>
                <input
                  type="text"
                  value={callsFilters.callingParty}
                  onChange={(e) => setCallsFilters((p) => ({ ...p, callingParty: e.target.value }))}
                />
              </label>
              <label className="field">
                <span className="field-label">Кому</span>
                <input
                  type="text"
                  value={callsFilters.calledParty}
                  onChange={(e) => setCallsFilters((p) => ({ ...p, calledParty: e.target.value }))}
                />
              </label>
              <label className="field">
                <span className="field-label">Call ID</span>
                <input
                  type="text"
                  value={callsFilters.callId}
                  onChange={(e) => setCallsFilters((p) => ({ ...p, callId: e.target.value }))}
                />
              </label>
              <label className="field">
                <span className="field-label">Направление</span>
                <select
                  value={callsFilters.direction}
                  onChange={(e) => setCallsFilters((p) => ({ ...p, direction: e.target.value }))}
                >
                  <option value="">Любое</option>
                  {directions.map((d) => (
                    <option key={d} value={d}>
                      {d}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field">
                <span className="field-label">Статус</span>
                <select
                  value={callsFilters.disposition}
                  onChange={(e) => setCallsFilters((p) => ({ ...p, disposition: e.target.value }))}
                >
                  <option value="">Любой</option>
                  {dispositions.map((d) => (
                    <option key={d} value={d}>
                      {d}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field">
                <span className="field-label">Тариф</span>
                <select
                  value={callsFilters.tariffId}
                  onChange={(e) => setCallsFilters((p) => ({ ...p, tariffId: e.target.value }))}
                >
                  <option value="">Любой</option>
                  {tariffIds.map((id) => (
                    <option key={id} value={id}>
                      #{id}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          )}
        </div>
      ) : null}

      <div className="results-body">
        {tab === 'summary' ? (
          <>
            <div className="table-wrap results-table">
              <table>
                <thead>
                  <tr>
                    <th>Телефон</th>
                    <th>Абонент</th>
                    <th>Звонков</th>
                    <th>Тарифицируемо, сек</th>
                    <th>Начислено</th>
                  </tr>
                </thead>
                <tbody>
                  {summaryPageItems.map((row: SubscriberSummary) => (
                    <tr key={row.phoneNumber}>
                      <td>{row.phoneNumber}</td>
                      <td>{row.clientName}</td>
                      <td>{row.callCount}</td>
                      <td>{row.totalBillableSec}</td>
                      <td>{formatMoney(row.totalCharge)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <Pagination
              page={summaryPageSafe}
              totalPages={summaryTotalPages}
              disabled={props.busy}
              onFirst={() => setSummaryPage(1)}
              onPrev={() => setSummaryPage((p) => Math.max(1, p - 1))}
              onNext={() => setSummaryPage((p) => Math.min(summaryTotalPages, p + 1))}
              onLast={() => setSummaryPage(summaryTotalPages)}
            />
          </>
        ) : (
          <>
            <div className="table-wrap results-table">
              <table>
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Call ID</th>
                    <th>Начало</th>
                    <th>Конец</th>
                    <th>Кто звонит</th>
                    <th>Кому</th>
                    <th>Направление</th>
                    <th>Статус</th>
                    <th>Длит., сек</th>
                    <th>Тарифиц., сек</th>
                    <th>Стоимость (расчёт)</th>
                    <th>Применённый тариф</th>
                  </tr>
                </thead>
                <tbody>
                  {callItemsFiltered.map((call: CallRecordDetail) => (
                    <tr key={call.id}>
                      <td>{call.id}</td>
                      <td>{call.callId}</td>
                      <td>{call.startTime}</td>
                      <td>{call.endTime}</td>
                      <td>{call.callingParty}</td>
                      <td>{call.calledParty}</td>
                      <td>{call.direction}</td>
                      <td>{call.disposition}</td>
                      <td>{call.durationSec}</td>
                      <td>{call.billableSec}</td>
                      <td>{call.computedCharge == null ? '' : formatMoney(call.computedCharge)}</td>
                      <td>
                        {call.appliedTariff ? (
                          <button
                            type="button"
                            className="linklike"
                            onClick={() => setSelectedTariff(call.appliedTariff ?? null)}
                            title="Показать тариф"
                          >
                            Тариф #{call.appliedTariff.id}
                          </button>
                        ) : (
                          '—'
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {selectedTariff ? (
              <div className="tariff-details" role="note" aria-label="Детали тарифа">
                <div className="tariff-details-row">
                  <strong>Тариф #{selectedTariff.id}</strong>
                  <button type="button" className="linklike" onClick={() => setSelectedTariff(null)}>
                    Скрыть
                  </button>
                </div>
                <div className="tariff-details-grid">
                  <div>
                    <span className="field-label">Префикс</span>
                    <div>{selectedTariff.prefix}</div>
                  </div>
                  <div>
                    <span className="field-label">Направление</span>
                    <div>{selectedTariff.destination}</div>
                  </div>
                  <div>
                    <span className="field-label">Ставка за минуту</span>
                    <div>{formatMoney(selectedTariff.ratePerMin)}</div>
                  </div>
                  <div>
                    <span className="field-label">Плата за соединение</span>
                    <div>{formatMoney(selectedTariff.connectionFee)}</div>
                  </div>
                </div>
              </div>
            ) : null}

            <Pagination
              page={props.model.callRecords?.page ?? callsPage}
              totalPages={props.model.callRecords?.totalPages ?? 1}
              disabled={props.busy}
              onFirst={() => setCallsPage(1)}
              onPrev={() => setCallsPage((p) => Math.max(1, p - 1))}
              onNext={() => setCallsPage((p) => Math.min(props.model.callRecords?.totalPages ?? p + 1, p + 1))}
              onLast={() => setCallsPage(props.model.callRecords?.totalPages ?? 1)}
            />
          </>
        )}
      </div>
    </section>
  )
}

