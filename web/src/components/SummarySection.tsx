import type { SubscriberSummary } from '../api'
import { formatMoney } from '../format'

interface SummarySectionProps {
  summary: SubscriberSummary[]
}

export function SummarySection({ summary }: SummarySectionProps) {
  if (summary.length === 0) {
    return null
  }

  return (
    <section className="panel">
      <h2>Итоговая сумма по абонентам</h2>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Телефон</th>
              <th>Абонент</th>
              <th>Звонков</th>
              <th>Секунд</th>
              <th>Начислено</th>
            </tr>
          </thead>
          <tbody>
            {summary.map((row) => (
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
    </section>
  )
}
