import { useState } from 'react'
import { onboardingApi } from '../../services/api'
import toast from 'react-hot-toast'

const STEPS = ['Hotel Info', 'PMS Setup', 'Channex', 'OTA Platforms', 'Sync']

const PMS_TYPES = [
  { value: 1, label: 'Sky Horizon' },
  { value: 2, label: 'Oracle Opera' },
  { value: 3, label: 'Mews' },
  { value: 4, label: 'Hotelogix' },
  { value: 5, label: 'eZee' },
  { value: 99, label: 'Manual (No PMS)' },
]

export default function OnboardingForm() {
  const [step, setStep] = useState(0)
  const [hotelId, setHotelId] = useState<number | null>(null)
  const [progress, setProgress] = useState(0)
  const [loading, setLoading] = useState(false)

  // Step forms
  const [hotelForm, setHotelForm] = useState({
    hotelName: '', city: 'Amman', country: 'Jordan', address: '',
    totalRooms: 50, pmsType: 1,
    adminEmail: '', adminPassword: '', adminFirstName: '', adminLastName: ''
  })

  const [pmsForm, setPmsForm] = useState({
    pmsType: 1, apiUrl: '', apiKey: '', username: '', password: ''
  })

  const [channexForm, setChannexForm] = useState({ apiKey: '', propertyId: '' })
  const [otaForms, setOtaForms] = useState({
    booking: { enabled: false, propertyId: '', channexChannelId: '' },
    airbnb: { enabled: false, propertyId: '', channexChannelId: '' },
    expedia: { enabled: false, propertyId: '', channexChannelId: '' },
  })

  // Step handlers
  const handleStep0 = async () => {
    setLoading(true)
    try {
      const { data } = await onboardingApi.start({
        hotel: {
          hotelName: hotelForm.hotelName,
          city: hotelForm.city,
          country: hotelForm.country,
          address: hotelForm.address,
          totalRooms: hotelForm.totalRooms,
          pmsType: hotelForm.pmsType,
        },
        adminEmail: hotelForm.adminEmail,
        adminPassword: hotelForm.adminPassword,
        adminFirstName: hotelForm.adminFirstName,
        adminLastName: hotelForm.adminLastName,
      })
      setHotelId(data.data.hotelId)
      setProgress(data.data.completionPercentage)
      toast.success('Hotel created! Proceed to PMS setup.')
      setStep(1)
    } catch (err: any) {
      toast.error(err.response?.data?.errors?.[0] ?? 'Failed to create hotel')
    } finally { setLoading(false) }
  }

  const handleStep1 = async () => {
    if (!hotelId) return
    setLoading(true)
    try {
      const { data } = await onboardingApi.configurePms(hotelId, { ...pmsForm, pmsType: pmsForm.pmsType })
      setProgress(data.data.completionPercentage)
      toast.success('PMS configured!')
      setStep(2)
    } catch {
      toast.error('Failed to configure PMS')
    } finally { setLoading(false) }
  }

  const handleStep2 = async () => {
    if (!hotelId) return
    setLoading(true)
    try {
      const { data } = await onboardingApi.connectChannex(hotelId, channexForm)
      setProgress(data.data.completionPercentage)
      toast.success('Channex connected!')
      setStep(3)
    } catch {
      toast.error('Failed to connect Channex')
    } finally { setLoading(false) }
  }

  const handleStep3 = async () => {
    if (!hotelId) return
    setLoading(true)
    try {
      if (otaForms.booking.enabled)
        await onboardingApi.activateOTA(hotelId, '1', { ...otaForms.booking, isViaChannex: true })
      if (otaForms.airbnb.enabled)
        await onboardingApi.activateOTA(hotelId, '2', { ...otaForms.airbnb, isViaChannex: true })
      if (otaForms.expedia.enabled)
        await onboardingApi.activateOTA(hotelId, '3', { ...otaForms.expedia, isViaChannex: true })
      toast.success('OTA platforms activated!')
      setStep(4)
    } catch {
      toast.error('Failed to activate OTAs')
    } finally { setLoading(false) }
  }

  const handleStep4 = async () => {
    if (!hotelId) return
    setLoading(true)
    try {
      const { data } = await onboardingApi.runSync(hotelId)
      setProgress(data.data.completionPercentage)
      toast.success('Initial sync complete! Your bookings are loading.')
    } catch {
      toast.error('Sync failed — check credentials and retry')
    } finally { setLoading(false) }
  }

  return (
    <div className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-lg w-full max-w-2xl p-8">
        {/* Header */}
        <h1 className="text-2xl font-bold text-gray-900 mb-2">Hotel Onboarding</h1>
        <p className="text-gray-500 text-sm mb-6">
          Connect your hotel to the booking management system in 5 steps.
        </p>

        {/* Progress */}
        <div className="mb-8">
          <div className="flex justify-between mb-2">
            {STEPS.map((s, i) => (
              <div key={s} className={`text-xs font-medium ${i === step ? 'text-blue-600' : i < step ? 'text-green-600' : 'text-gray-400'}`}>
                {i < step ? '✓' : i + 1}. {s}
              </div>
            ))}
          </div>
          <div className="h-2 bg-gray-100 rounded-full">
            <div className="h-2 bg-blue-600 rounded-full transition-all"
              style={{ width: `${progress || (step / (STEPS.length - 1)) * 100}%` }} />
          </div>
        </div>

        {/* Step 0: Hotel Info */}
        {step === 0 && (
          <div className="space-y-4">
            <h2 className="font-semibold text-gray-800">Hotel Information</h2>
            <div className="grid grid-cols-2 gap-4">
              <div className="col-span-2">
                <Field label="Hotel Name *" value={hotelForm.hotelName}
                  onChange={(v) => setHotelForm({ ...hotelForm, hotelName: v })} />
              </div>
              <Field label="City *" value={hotelForm.city}
                onChange={(v) => setHotelForm({ ...hotelForm, city: v })} />
              <Field label="Country *" value={hotelForm.country}
                onChange={(v) => setHotelForm({ ...hotelForm, country: v })} />
              <Field label="Address" value={hotelForm.address}
                onChange={(v) => setHotelForm({ ...hotelForm, address: v })} />
              <Field label="Total Rooms" value={String(hotelForm.totalRooms)} type="number"
                onChange={(v) => setHotelForm({ ...hotelForm, totalRooms: +v })} />
              <div className="col-span-2">
                <label className="block text-xs font-medium text-gray-700 mb-1">PMS System</label>
                <select value={hotelForm.pmsType}
                  onChange={(e) => setHotelForm({ ...hotelForm, pmsType: +e.target.value })}
                  className="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
                  {PMS_TYPES.map(p => <option key={p.value} value={p.value}>{p.label}</option>)}
                </select>
              </div>
            </div>
            <h2 className="font-semibold text-gray-800 pt-2">Admin Account</h2>
            <div className="grid grid-cols-2 gap-4">
              <Field label="First Name *" value={hotelForm.adminFirstName}
                onChange={(v) => setHotelForm({ ...hotelForm, adminFirstName: v })} />
              <Field label="Last Name *" value={hotelForm.adminLastName}
                onChange={(v) => setHotelForm({ ...hotelForm, adminLastName: v })} />
              <div className="col-span-2">
                <Field label="Email *" value={hotelForm.adminEmail} type="email"
                  onChange={(v) => setHotelForm({ ...hotelForm, adminEmail: v })} />
              </div>
              <div className="col-span-2">
                <Field label="Password *" value={hotelForm.adminPassword} type="password"
                  onChange={(v) => setHotelForm({ ...hotelForm, adminPassword: v })} />
              </div>
            </div>
            <Btn onClick={handleStep0} loading={loading}>Create Hotel & Continue →</Btn>
          </div>
        )}

        {/* Step 1: PMS */}
        {step === 1 && (
          <div className="space-y-4">
            <h2 className="font-semibold text-gray-800">PMS Configuration</h2>
            <Field label="API URL" value={pmsForm.apiUrl} type="url"
              onChange={(v) => setPmsForm({ ...pmsForm, apiUrl: v })}
              placeholder="https://api.yourpms.com/v2/" />
            <Field label="API Key" value={pmsForm.apiKey} type="password"
              onChange={(v) => setPmsForm({ ...pmsForm, apiKey: v })} />
            <div className="grid grid-cols-2 gap-3">
              <Field label="Username" value={pmsForm.username}
                onChange={(v) => setPmsForm({ ...pmsForm, username: v })} />
              <Field label="Password" value={pmsForm.password} type="password"
                onChange={(v) => setPmsForm({ ...pmsForm, password: v })} />
            </div>
            <Btn onClick={handleStep1} loading={loading}>Save PMS & Continue →</Btn>
          </div>
        )}

        {/* Step 2: Channex */}
        {step === 2 && (
          <div className="space-y-4">
            <h2 className="font-semibold text-gray-800">Channex Channel Manager</h2>
            <p className="text-sm text-gray-500">
              Channex connects your hotel to 50+ booking platforms. Enter your Channex API credentials.
            </p>
            <Field label="Channex API Key *" value={channexForm.apiKey} type="password"
              onChange={(v) => setChannexForm({ ...channexForm, apiKey: v })} />
            <Field label="Channex Property ID *" value={channexForm.propertyId}
              onChange={(v) => setChannexForm({ ...channexForm, propertyId: v })}
              placeholder="Found in your Channex dashboard" />
            <Btn onClick={handleStep2} loading={loading}>Connect Channex →</Btn>
          </div>
        )}

        {/* Step 3: OTAs */}
        {step === 3 && (
          <div className="space-y-4">
            <h2 className="font-semibold text-gray-800">Select OTA Platforms</h2>
            {(['booking', 'airbnb', 'expedia'] as const).map((ota) => (
              <div key={ota} className="border rounded-xl p-4 space-y-3">
                <label className="flex items-center gap-2 font-medium">
                  <input type="checkbox" checked={otaForms[ota].enabled}
                    onChange={(e) => setOtaForms({
                      ...otaForms,
                      [ota]: { ...otaForms[ota], enabled: e.target.checked }
                    })} />
                  {{ booking: '🏨 Booking.com', airbnb: '🏠 Airbnb', expedia: '✈️ Expedia' }[ota]}
                </label>
                {otaForms[ota].enabled && (
                  <div className="grid grid-cols-2 gap-3 pt-2">
                    <Field label="Property ID" value={otaForms[ota].propertyId}
                      onChange={(v) => setOtaForms({
                        ...otaForms, [ota]: { ...otaForms[ota], propertyId: v }
                      })} />
                    <Field label="Channex Channel ID" value={otaForms[ota].channexChannelId}
                      onChange={(v) => setOtaForms({
                        ...otaForms, [ota]: { ...otaForms[ota], channexChannelId: v }
                      })} />
                  </div>
                )}
              </div>
            ))}
            <Btn onClick={handleStep3} loading={loading}>Activate Platforms →</Btn>
          </div>
        )}

        {/* Step 4: Sync */}
        {step === 4 && (
          <div className="space-y-6 text-center">
            <div className="text-6xl">🎉</div>
            <h2 className="text-xl font-bold text-gray-900">Almost done!</h2>
            <p className="text-gray-500">
              Run the initial sync to pull all existing bookings from your PMS and OTA platforms.
            </p>
            <Btn onClick={handleStep4} loading={loading}>Run Initial Sync →</Btn>
            {progress === 100 && (
              <a href="/dashboard" className="block text-blue-600 hover:underline text-sm">
                Go to Dashboard →
              </a>
            )}
          </div>
        )}
      </div>
    </div>
  )
}

function Field({ label, value, onChange, type = 'text', placeholder }: {
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

function Btn({ children, onClick, loading }: {
  children: React.ReactNode; onClick: () => void; loading: boolean
}) {
  return (
    <button onClick={onClick} disabled={loading}
      className="w-full bg-blue-600 text-white py-2.5 rounded-xl font-medium hover:bg-blue-700 disabled:opacity-50 transition-colors">
      {loading ? 'Please wait...' : children}
    </button>
  )
}
