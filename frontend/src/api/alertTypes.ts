// Mirrors backend/JobsGeParser/Endpoints/Dtos/AlertApiDtos.cs

export type AlertCadence = 'Instant' | 'DailyDigest'

export type AlertChannel = 'Email' | 'Sms'

export type AlertSubscriptionStatus = 'PendingConfirmation' | 'Active' | 'Paused' | 'Deleted'

export type AlertSubscriberStatus = 'Pending' | 'Confirmed' | 'Unsubscribed' | 'Suppressed'

export interface AlertCriteria {
  query: string | null
  titleContains: string | null
  descriptionContains: string | null
  companyContains: string | null
  categorySlugs: string[] | null
  cities: string[] | null
  workModes: string[] | null
  employmentTypes: string[] | null
  seniorities: string[] | null
  languages: string[] | null
  salaryMin: number | null
  salaryMax: number | null
  salaryCurrency: string | null
  requireSalary: boolean
}

export interface AlertCriteriaOptions {
  categorySlugs: string[]
  cities: string[]
  workModes: string[]
  employmentTypes: string[]
  seniorities: string[]
  languages: string[]
  maxTextLength: number
  maxArrayLength: number
}

export interface CreateAlertRequest {
  channel: AlertChannel
  address: string
  name: string
  cadence: AlertCadence
  /** Wall clock time in the subscriber's timezone, formatted HH:mm:ss. */
  digestSendLocalTime: string | null
  /** IANA zone, e.g. Asia/Tbilisi. */
  timeZoneId: string | null
  criteria: AlertCriteria
}

export interface CreateAlertResponse {
  status: string
  currentlyMatchingJobs: number
}

export interface ConfirmAlertResponse {
  manageToken: string
  manageUrl: string
  activatedSubscriptions: number
}

export interface AlertSubscription {
  id: string
  name: string
  status: AlertSubscriptionStatus | string
  cadence: AlertCadence
  digestSendLocalTime: string
  channel: AlertChannel
  maskedAddress: string
  createdAt: string
  matchFromAt: string
  lastAlertSentAt: string | null
  alertsSent: number
  criteria: AlertCriteria
}

export interface AlertSubscriptionsResponse {
  channel: AlertChannel
  maskedAddress: string
  subscriberStatus: AlertSubscriberStatus | string
  subscriptions: AlertSubscription[]
}

export interface UpdateAlertRequest {
  name?: string
  cadence?: AlertCadence
  digestSendLocalTime?: string
  criteria?: AlertCriteria
}

/** Ops view of one subscription - carries the real address, no masking. */
export interface OpsAlertSubscription {
  id: string
  name: string
  status: AlertSubscriptionStatus | string
  cadence: AlertCadence
  digestSendLocalTime: string
  subscriberId: string
  channel: AlertChannel
  address: string
  subscriberStatus: AlertSubscriberStatus | string
  createdAt: string
  matchFromAt: string
  lastAlertSentAt: string | null
  alertsSent: number
  criteria: AlertCriteria
}

export interface OpsAlertSubscriptionsPage {
  items: OpsAlertSubscription[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface OpsAlertsQueryParams {
  status?: string
  cadence?: AlertCadence
  channel?: AlertChannel
  q?: string
  page?: number
  pageSize?: number
}
