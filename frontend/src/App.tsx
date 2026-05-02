import { BrowserRouter, Routes, Route, Navigate, Outlet, Link } from 'react-router-dom'
import { Provider, useSelector, useDispatch } from 'react-redux'
import { Toaster } from 'react-hot-toast'
import { store, RootState, AppDispatch } from './store'
import { logout } from './store/slices/authSlice'
import LoginPage from './pages/LoginPage'
import Dashboard from './components/dashboard/Dashboard'
import BookingsList from './components/booking/BookingsList'
import CredentialsSetup from './components/credentials/CredentialsSetup'
import OnboardingForm from './components/onboarding/OnboardingForm'

function RequireAuth({ children }: { children: React.ReactNode }) {
  const { user } = useSelector((s: RootState) => s.auth)
  if (!user) return <Navigate to="/login" replace />
  return <>{children}</>
}

function Layout() {
  const { user } = useSelector((s: RootState) => s.auth)
  const dispatch = useDispatch<AppDispatch>()

  return (
    <div className="flex h-screen bg-gray-100">
      {/* Sidebar */}
      <aside className="w-64 bg-blue-900 text-white flex flex-col">
        <div className="p-6 border-b border-blue-800">
          <h1 className="text-xl font-bold">🏨 HotelSaaS</h1>
          <p className="text-blue-300 text-xs mt-1">{user?.fullName}</p>
        </div>
        <nav className="flex-1 p-4 space-y-1">
          <NavLink to="/dashboard" icon="📊">Dashboard</NavLink>
          <NavLink to="/bookings" icon="📋">All Bookings</NavLink>
          {user?.role !== 'Staff' && (
            <NavLink to="/credentials" icon="🔑">Credentials</NavLink>
          )}
          {user?.role === 'SuperAdmin' && (
            <NavLink to="/onboarding" icon="➕">Add Hotel</NavLink>
          )}
        </nav>
        <div className="p-4 border-t border-blue-800">
          <p className="text-blue-300 text-xs mb-2">{user?.role}</p>
          <button onClick={() => dispatch(logout())}
            className="w-full text-left text-sm text-blue-300 hover:text-white transition-colors">
            🚪 Sign Out
          </button>
        </div>
      </aside>

      {/* Main */}
      <main className="flex-1 overflow-auto">
        <Outlet />
      </main>
    </div>
  )
}

function NavLink({ to, icon, children }: { to: string; icon: string; children: React.ReactNode }) {
  return (
    <Link to={to}
      className="flex items-center gap-3 px-3 py-2.5 rounded-xl text-blue-100 hover:bg-blue-800 hover:text-white transition-colors text-sm">
      <span>{icon}</span>
      <span>{children}</span>
    </Link>
  )
}

function AppRoutes() {
  const { user } = useSelector((s: RootState) => s.auth)
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/onboarding" element={<OnboardingForm />} />
      <Route element={<RequireAuth><Layout /></RequireAuth>}>
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/bookings" element={
          <div className="p-6"><BookingsList /></div>
        } />
        <Route path="/credentials" element={
          user?.hotelId
            ? <CredentialsSetup hotelId={user.hotelId} />
            : <div className="p-6 text-gray-500">Select a hotel first</div>
        } />
        <Route path="/" element={<Navigate to="/dashboard" replace />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default function App() {
  return (
    <Provider store={store}>
      <BrowserRouter>
        <Toaster position="top-right" />
        <AppRoutes />
      </BrowserRouter>
    </Provider>
  )
}
