import type { UploadKind } from './api'

export interface UploadTemplate {
  kind: UploadKind
  title: string
  hint: string
  accept: string
}

export const uploadTemplates: UploadTemplate[] = [
  {
    kind: 'cdr',
    title: 'Загрузка CDR-файла',
    hint: 'Формат: *.txt, разделитель "|"',
    accept: '.txt,.csv,text/plain',
  },
  {
    kind: 'tariff',
    title: 'Загрузка тарифа',
    hint: 'Формат: CSV с разделителем ";"',
    accept: '.csv,text/csv',
  },
  {
    kind: 'subscribers',
    title: 'Загрузка абонентской базы',
    hint: 'Формат: CSV с разделителем ";"',
    accept: '.csv,text/csv',
  },
]
