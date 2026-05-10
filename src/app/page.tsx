import Link from 'next/link'
import {
  Shield,
  Clock,
  Eye,
  Link2,
  Upload,
  BarChart3,
  CheckCircle2,
  ArrowRight,
  Lock,
} from 'lucide-react'

const features = [
  {
    icon: Upload,
    title: 'Secure File Upload',
    desc: 'Upload PDFs, Word docs, images, and more up to 100MB. Files are stored encrypted in AWS S3.',
  },
  {
    icon: Link2,
    title: 'Unique Secure Links',
    desc: 'Every file gets a unique, unguessable token link. No login required for recipients.',
  },
  {
    icon: Shield,
    title: 'Instant Revocation',
    desc: 'Revoke access to any shared document instantly. Links become inaccessible immediately.',
  },
  {
    icon: Clock,
    title: 'Expiry Controls',
    desc: 'Set links to expire after X days, after first view, or revoke manually at any time.',
  },
  {
    icon: Eye,
    title: 'Access Tracking',
    desc: 'See who viewed your documents, when, and how many times. Real-time access logs.',
  },
  {
    icon: BarChart3,
    title: 'Sender Dashboard',
    desc: 'Manage all your shared documents in one place with full status and analytics.',
  },
]

const steps = [
  { number: '01', title: 'Upload your document', desc: 'Drag and drop or browse files up to 100MB.' },
  { number: '02', title: 'Configure sharing', desc: 'Set expiry, add recipient email, and write a message.' },
  { number: '03', title: 'Send secure link', desc: 'Share via email or copy the link to send yourself.' },
  { number: '04', title: 'Stay in control', desc: 'Track views and revoke access anytime from your dashboard.' },
]

export default function HomePage() {
  return (
    <div className="min-h-screen bg-white">
      {/* Nav */}
      <nav className="border-b border-gray-100 bg-white/80 backdrop-blur-sm sticky top-0 z-50">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 h-16 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 bg-brand-600 rounded-lg flex items-center justify-center">
              <Lock className="w-4 h-4 text-white" />
            </div>
            <span className="font-bold text-gray-900 text-lg">SecureDocs</span>
          </div>
          <div className="flex items-center gap-3">
            <Link
              href="/login"
              className="text-sm font-medium text-gray-600 hover:text-gray-900 transition-colors"
            >
              Sign in
            </Link>
            <Link
              href="/register"
              className="text-sm font-medium bg-brand-600 text-white px-4 py-2 rounded-lg hover:bg-brand-700 transition-colors"
            >
              Get started free
            </Link>
          </div>
        </div>
      </nav>

      {/* Hero */}
      <section className="pt-20 pb-24 px-4 sm:px-6">
        <div className="max-w-4xl mx-auto text-center">
          <div className="inline-flex items-center gap-2 bg-brand-50 text-brand-700 text-sm font-medium px-4 py-2 rounded-full mb-8">
            <Shield className="w-4 h-4" />
            Secure Document Delivery
          </div>
          <h1 className="text-5xl sm:text-6xl font-extrabold text-gray-900 leading-tight mb-6">
            Share documents.<br />
            <span className="text-brand-600">Stay in control.</span>
          </h1>
          <p className="text-xl text-gray-500 max-w-2xl mx-auto mb-10 leading-relaxed">
            Send sensitive files with secure links, then revoke access at any time.
            Perfect for legal, finance, and creative professionals.
          </p>
          <div className="flex flex-col sm:flex-row gap-3 justify-center">
            <Link
              href="/register"
              className="inline-flex items-center justify-center gap-2 bg-brand-600 text-white font-semibold px-8 py-4 rounded-xl hover:bg-brand-700 transition-colors text-base"
            >
              Start sharing securely
              <ArrowRight className="w-4 h-4" />
            </Link>
            <Link
              href="/login"
              className="inline-flex items-center justify-center gap-2 bg-gray-100 text-gray-700 font-semibold px-8 py-4 rounded-xl hover:bg-gray-200 transition-colors text-base"
            >
              Sign in
            </Link>
          </div>
        </div>
      </section>

      {/* Feature grid */}
      <section className="py-20 bg-gray-50 px-4 sm:px-6">
        <div className="max-w-6xl mx-auto">
          <div className="text-center mb-14">
            <h2 className="text-3xl font-bold text-gray-900 mb-4">Everything you need to share safely</h2>
            <p className="text-lg text-gray-500 max-w-xl mx-auto">
              No more "did they get it?" or "can I take that back?" — full control from send to revoke.
            </p>
          </div>
          <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-6">
            {features.map((f) => (
              <div key={f.title} className="bg-white rounded-2xl p-6 border border-gray-100 hover:border-brand-200 hover:shadow-sm transition-all">
                <div className="w-10 h-10 bg-brand-50 rounded-xl flex items-center justify-center mb-4">
                  <f.icon className="w-5 h-5 text-brand-600" />
                </div>
                <h3 className="font-semibold text-gray-900 mb-2">{f.title}</h3>
                <p className="text-gray-500 text-sm leading-relaxed">{f.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* How it works */}
      <section className="py-20 px-4 sm:px-6">
        <div className="max-w-4xl mx-auto">
          <div className="text-center mb-14">
            <h2 className="text-3xl font-bold text-gray-900 mb-4">How it works</h2>
          </div>
          <div className="space-y-0">
            {steps.map((step, i) => (
              <div key={step.number} className="flex gap-8 pb-10">
                <div className="flex flex-col items-center">
                  <div className="w-12 h-12 rounded-2xl bg-brand-600 text-white font-bold text-sm flex items-center justify-center flex-shrink-0">
                    {step.number}
                  </div>
                  {i < steps.length - 1 && (
                    <div className="w-px flex-1 bg-brand-200 mt-3" />
                  )}
                </div>
                <div className="pt-2 pb-6">
                  <h3 className="font-semibold text-gray-900 text-lg mb-1">{step.title}</h3>
                  <p className="text-gray-500">{step.desc}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Who it's for */}
      <section className="py-20 bg-brand-600 px-4 sm:px-6">
        <div className="max-w-4xl mx-auto text-center">
          <h2 className="text-3xl font-bold text-white mb-4">Built for professionals who share sensitive files</h2>
          <p className="text-brand-200 text-lg mb-10">
            Legal teams, finance advisors, film & media, and anyone who needs control over shared documents.
          </p>
          <div className="flex flex-wrap gap-3 justify-center mb-10">
            {['Legal & Compliance', 'Finance & Accounting', 'Film & Media', 'Real Estate', 'Healthcare', 'Consulting'].map((tag) => (
              <span key={tag} className="bg-white/20 text-white text-sm font-medium px-4 py-2 rounded-full">
                {tag}
              </span>
            ))}
          </div>
          <Link
            href="/register"
            className="inline-flex items-center gap-2 bg-white text-brand-700 font-semibold px-8 py-4 rounded-xl hover:bg-brand-50 transition-colors"
          >
            <CheckCircle2 className="w-5 h-5" />
            Get started — it's free
          </Link>
        </div>
      </section>

      {/* Footer */}
      <footer className="border-t border-gray-100 py-10 px-4 sm:px-6">
        <div className="max-w-6xl mx-auto flex flex-col sm:flex-row items-center justify-between gap-4">
          <div className="flex items-center gap-2">
            <div className="w-6 h-6 bg-brand-600 rounded-md flex items-center justify-center">
              <Lock className="w-3 h-3 text-white" />
            </div>
            <span className="font-semibold text-gray-800">SecureDocs</span>
          </div>
          <p className="text-sm text-gray-400">© {new Date().getFullYear()} SecureDocs. All rights reserved.</p>
          <div className="flex gap-5">
            <Link href="/login" className="text-sm text-gray-400 hover:text-gray-600">Sign in</Link>
            <Link href="/register" className="text-sm text-gray-400 hover:text-gray-600">Register</Link>
          </div>
        </div>
      </footer>
    </div>
  )
}
