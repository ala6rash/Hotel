import { useEffect, useState } from 'react'
import { credentialsApi } from '../../services/api'
import toast from 'react-hot-toast'

interface Props { hotelId: number }

const PMS_TYPES = [
  { value: 1, label: 'Sky Horizon' },
  { value: 2, label: 'Oracle Opera' },
  { value: 3, label: 'Mews' },
  { value: 4, label: 'Hotelogix' },
  { value: 5, label: 'eZee' },
  { value: 6, label: 'Protel' },
]

const OTA_TYPES = [
  { value: 1, label: '🏨 Booking.com' },
  { value: 2, label: '🏠 Airbnb' },
  { value: 3, label: '✈️ Expedia' },
  { value: 4, label: '🌏 Agoda' },
]

export default function CredentialsSetup({ hotelId }: Props) {
  const [otaCreds, setOtaCreds] = useState<any[]>([])
  const [pmsCreds, setPmsCreds] = useState<any[]>([])
  const [activeTab, setActiveTab] = useState<'pms' | 'ota'>('pms')
  const [testing, setTesting] = useState<number | null>(null)

  // PMS form
  const [pmsForm, setPmsForm] = useState({
    pmsType: 1, apiUrl: '', apiKey: '', username: '', password: '',
    clientId: '', clientSecret: ''
  })

  // OTA form
  const [otaForm, setOtaForm] = useState({
    otaType: 1, propertyId: '', apiKey: '', channexChannelId: '',
    isViaChannex: true
  })

  useEffect(() => {
    credentialsApi.getOTA(hotelId).then(({ data }) => setOtaCreds(data.data))
    credentialsApi.getPMS(hotelId).then(({ data }) => setPmsCreds(data.data))
  }, [hotelId])

  const savePMS = async () => {
    try {
      await credentialsApi.savePMS({ ...pmsForm, hotelId })
      toast.success('PMS credentials saved and encrypted')
      const { data } = await credentialsApi.getPMS(hotelId)
      setPmsCreds(data.data)
    } catch {
      toast.error('Failed to save PMS credentials')
    }
  }

  const saveOTA = async () => {
    try {
      await credentialsApi.saveOTA({ ...otaForm, hotelId })
      toast.success('OTA credentials saved and encrypted')
      const { data } = await credentialsApi.getOTA(hotelId)
      setOtaCreds(data.data)
    } catch {
      toast.error('Failed to save OTA credentials')
    }
  }

  const testPMS = async (credId: number) => {
    setTesting(credId)
    try {
      const { data } = await credentialsApi.testPMS(credId)
      if (data.data.isConnected) toast.success(data.data.message)
      else toast.error(data.data.message)
      const updated = await credentialsApi.getPMS(hotelId)
      setPmsCreds(updated.data.data)
    } catch {
      toast.error('Connection test failed')
    } finally {
      setTesting(null)
    }
  }

  const testOTA = async (credId: number) => {
    setTesting(credId)
    try {
      const { data } = await credentialsApi.testOTA(credId)
      if (data.data.isConnected) toast.success(data.data.message)
      else toast.error(data.data.message)
      const updated = await credentialsApi.getOTA(hotelId)
      setOtaCreds(updated.data.data)
    } catch {
      toast.error('Connection test failed')
    } finally {
      setTesting(null)
    }
  }

  return (
    <div className="p-6 space-y-6">
      <h1 className="text-2xl font-bold text-gray-900">Credentials Management</h1>
      <p className="text-sm text-gray-500">
        All credentials are encrypted with AES-256 before storage. Keys are never displayed after saving.
      </p>

      {/* Tabs */}
      <div className="flex border-b">
        {(['pms', 'ota'] as const).map((tab) => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab)}
            className={`px-6 py-3 font-medium text-sm border-b-2 transition-colors ${
              activeTab === tab
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            {tab === 'pms' ? '🖥️ PMS System' : '🌐 OTA Platforms'}
          </button>
        ))}
      </div>

      {activeTab === 'pms' && (
        <div className="grid lg:grid-cols-2 gap-6">
          {/* PMS Form */}
          <div className="bg-white rounded-xl border p-5 space-y-4">
            <h3 className="font-semibold text-gray-900">Add / Update PMS Credentials</h3>

            <Select label="PMS System" value={pmsForm.pmsType}
              onChange={(v) => setPmsForm({ ...pmsForm, pmsType: +v })}
              options={PMS_TYPES} />

            <Input label="API URL" value={pmsForm.apiUrl} type="url"
              onChange={(v) => setPmsForm({ ...pmsForm, apiUrl: v })}
              placeholder="https://api.yourpms.com/v2/" />

            <Input label="API Key" value={pmsForm.apiKey} type="password"
              onChange={(v) => setPmsForm({ ...pmsForm, apiKey: v })} />

            <div className="grid grid-cols-2 gap-3">
              <Input label="Username" value={pmsForm.username}
                onChange={(v) => setPmsForm({ ...pmsForm, username: v })} />
              <Input label="Password" value={pmsForm.password} type="password"
                onChange={(v) => setPmsForm({ ...pmsForm, password: v })} />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <Input label="Client ID (OAuth)" value={pmsForm.clientId}
                onChange={(v) => setPmsForm({ ...pmsForm, clientId: v })} />
              <Input label="Client Secret" value={pmsForm.clientSecret} type="password"
                onChange={(v) => setPmsForm({ ...pmsForm, clientSecret: v })} />
            </div>

            <button onClick={savePMS}
              className="w-full bg-blue-600 text-white py-2 rounded-lg hover:bg-blue-700 font-medium">
              🔒 Save & Encrypt PMS Credentials
            </button>
          </div>

          {/* PMS list */}
          <div className="space-y-3">
            <h3 className="font-semibold text-gray-900">Connected PMS Systems</h3>
            {pmsCreds.length === 0 ? (
              <p className="text-gray-400 text-sm">No PMS connected yet</p>
            ) : pmsCreds.map((c) => (
              <CredentialCard
                key={c.credentialId}
                name={PMS_TYPES.find(p => p.value === c.pmsType)?.label ?? c.pmsType}
                isActive={c.isActive}
                lastTest={c.lastTestedDate}
                lastResult={c.lastTestResult}
                error={c.lastTestError}
                onTest={() => testPMS(c.credentialId)}
                testing={testing === c.credentialId}
              />
            ))}
          </div>
        </div>
      )}

      {activeTab === 'ota' && (
        <div className="grid lg:grid-cols-2 gap-6">
          {/* OTA Form */}
          <div className="bg-white rounded-xl border p-5 space-y-4">
            <h3 className="font-semibold text-gray-900">Add / Update OTA Connection</h3>

            <Select label="OTA Platform" value={otaForm.otaType}
              onChange={(v) => setOtaForm({ ...otaForm, otaType: +v })}
              options={OTA_TYPES} />

            <Input label="Property ID" value={otaForm.propertyId}
              onChange={(v) => setOtaForm({ ...otaForm, propertyId: v })}
              placeholder="Your property ID on this platform" />

            <Input label="Channex Channel ID" value={otaForm.channexChannelId}
              onChange={(v) => setOtaForm({ ...otaForm, channexChannelId: v })}
              placeholder="From Channex dashboard" />

            <Input label="API Key (if direct)" value={otaForm.apiKey} type="password"
              onChange={(v) => setOtaForm({ ...otaForm, apiKey: v })} />

            <label className="flex items-center gap-2 text-sm text-gray-700">
              <input type="checkbox" checked={otaForm.isViaChannex}
                onChange={(e) => setOtaForm({ ...otaForm, isViaChannex: e.target.checked })} />
              Connect via Channex (recommended)
            </label>

            <button onClick={saveOTA}
              className="w-full bg-blue-600 text-white py-2 rounded-lg hover:bg-blue-700 font-medium">
              🔒 Save & Encrypt OTA Credentials
            </button>
          </div>

          {/* OTA list */}
          <div className="space-y-3">
            <h3 className="font-semibold text-gray-900">Connected OTA Platforms</h3>
            {otaCreds.length === 0 ? (
              <p className="text-gray-400 text-sm">No OTA platforms connected yet</p>
            ) : otaCreds.map((c) => (
              <CredentialCard
                key={c.credentialId}
                name={OTA_TYPES.find(o => o.value === c.otaType)?.label ?? c.otaType}
                isActive={c.isActive}
                lastTest={c.lastTestedDate}
                lastResult={c.lastTestResult}
                error={c.lastTestError}
                onTest={() => testOTA(c.credentialId)}
                testing={testing === c.credentialId}
              />
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

// ─── Sub-components ────────────────────────────────────────────────────────────

function Input({ label, value, onChange, type = 'text', placeholder }: {
  label: string; value: string; onChange: (v: string) => void
  type?: string; placeholder?: string
}) {
  return (
    <div>
      <label className="block text-xs font-medium text-gray-700 mb-1">{label}</label>
      <input type={type} value={value} placeholder={placeholder}
        onChange={(e) => onChange(e.target.value)}
        className="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
    </div>
  )
}

function Select({ label, value, onChange, options }: {
  label: string; value: number; onChange: (v: string) => void
  options: { value: number; label: string }[]
}) {
  return (
    <div>
      <label className="block text-xs font-medium text-gray-700 mb-1">{label}</label>
      <select value={value} onChange={(e) => onChange(e.target.value)}
        className="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
        {options.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
    </div>
  )
}

function CredentialCard({ name, isActive, lastTest, lastResult, error, onTest, testing }: {
  name: string; isActive: boolean; lastTest?: string; lastResult?: boolean
  error?: string; onTest: () => void; testing: boolean
}) {
  return (
    <div className="bg-white rounded-xl border p-4 space-y-2">
      <div className="flex items-center justify-between">
        <span className="font-medium text-gray-900">{name}</span>
        <span className={`text-xs px-2 py-0.5 rounded-full ${isActive ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'}`}>
          {isActive ? 'Active' : 'Inactive'}
        </span>
      </div>
      {lastTest && (
        <div className="flex items-center gap-2 text-xs text-gray-500">
          <span>{lastResult ? '✅' : '❌'}</span>
          <span>Last tested: {new Date(lastTest).toLocaleString()}</span>
        </div>
      )}
      {error && <p className="text-xs text-red-500">{error}</p>}
      <button onClick={onTest} disabled={testing}
        className="text-sm text-blue-600 hover:text-blue-800 disabled:opacity-50">
        {testing ? 'Testing...' : '🔌 Test Connection'}
      </button>
    </div>
  )
}
