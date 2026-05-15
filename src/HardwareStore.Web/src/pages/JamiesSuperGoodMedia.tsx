import { Link } from 'react-router-dom';

const shareUrl = 'https://hardwarestore-develop-web.happysand-3d660167.westus3.azurecontainerapps.io/jamies-super-good-media';

const services = [
  {
    title: 'Drone footage',
    description: 'Sweeping aerial video and stills that give buyers instant context on curb appeal, lot size, and neighborhood position.',
  },
  {
    title: 'Photo + video packages',
    description: 'Clean interiors, flattering exterior coverage, vertical social edits, and polished walk-throughs built for modern listings.',
  },
  {
    title: 'Commercial-ready coverage',
    description: 'Media plans for offices, retail, multifamily, and development sites with assets tailored to brokers, investors, and leasing teams.',
  },
];

const workflow = [
  'Flexible coverage for residential and commercial listings',
  'Fast turnaround for launch-day marketing',
  'Brand-ready edits for MLS, web, and social channels',
];

function HeroArtwork() {
  return (
    <svg viewBox="0 0 640 520" className="w-full h-auto" role="img" aria-label="Happy homeowners in front of a house with a for sale sign">
      <defs>
        <linearGradient id="sky" x1="0%" x2="100%" y1="0%" y2="100%">
          <stop offset="0%" stopColor="#0f172a" />
          <stop offset="100%" stopColor="#111827" />
        </linearGradient>
        <linearGradient id="panel" x1="0%" x2="100%" y1="0%" y2="100%">
          <stop offset="0%" stopColor="#1e293b" />
          <stop offset="100%" stopColor="#0f172a" />
        </linearGradient>
      </defs>
      <rect width="640" height="520" rx="32" fill="url(#sky)" />
      <circle cx="510" cy="90" r="34" fill="#fde68a" opacity="0.95" />
      <path d="M84 385h470" stroke="#334155" strokeWidth="10" strokeLinecap="round" />
      <path d="M175 222 318 118l144 104v160H175Z" fill="#e2e8f0" />
      <path d="M212 232h213v150H212Z" fill="#f8fafc" />
      <path d="M246 382V274h54v108" fill="#cbd5e1" />
      <path d="M330 264h58v50h-58zM330 329h58v53h-58z" fill="#bfdbfe" />
      <path d="M188 230 318 136l130 94" fill="none" stroke="#94a3b8" strokeWidth="14" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M490 215v140" stroke="#cbd5e1" strokeWidth="12" strokeLinecap="round" />
      <rect x="490" y="215" width="100" height="74" rx="10" fill="#ffffff" />
      <text x="540" y="248" textAnchor="middle" fontSize="22" fontWeight="700" fill="#0f172a">FOR</text>
      <text x="540" y="274" textAnchor="middle" fontSize="22" fontWeight="700" fill="#0f172a">SALE</text>
      <circle cx="225" cy="340" r="22" fill="#f8d1b5" />
      <path d="M198 390c7-28 23-43 47-43s40 15 47 43" fill="#8b5cf6" />
      <path d="M208 336c10-16 26-18 35-8 9 9 5 24-5 31" fill="#1e293b" />
      <circle cx="365" cy="335" r="22" fill="#f6c7a2" />
      <path d="M338 387c8-27 24-41 48-41s40 14 48 41" fill="#0ea5e9" />
      <path d="M352 334c8-14 24-21 39-12 9 5 14 15 13 25" fill="#312e81" />
      <path d="M229 370c27 12 46 12 71 0" stroke="#22c55e" strokeWidth="8" strokeLinecap="round" />
      <path d="M372 366c16 8 30 8 46 0" stroke="#22c55e" strokeWidth="8" strokeLinecap="round" />
      <path d="M430 154h74l44 30-44 29h-74z" fill="#38bdf8" opacity="0.9" />
      <circle cx="468" cy="184" r="18" fill="#0f172a" />
      <path d="M468 184l29-14" stroke="#38bdf8" strokeWidth="6" strokeLinecap="round" />
      <circle cx="503" cy="168" r="6" fill="#38bdf8" />
      <rect x="70" y="58" width="170" height="64" rx="18" fill="url(#panel)" stroke="#334155" />
      <text x="155" y="88" textAnchor="middle" fontSize="22" fontWeight="700" fill="#f8fafc">JSGM</text>
      <text x="155" y="110" textAnchor="middle" fontSize="14" fill="#cbd5e1">real estate media</text>
    </svg>
  );
}

export default function JamiesSuperGoodMedia() {
  return (
    <div className="min-h-screen bg-[#050816] text-white">
      <section className="relative overflow-hidden">
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_left,_rgba(56,189,248,0.18),_transparent_34%),radial-gradient(circle_at_top_right,_rgba(168,85,247,0.22),_transparent_30%),linear-gradient(180deg,_rgba(5,8,22,0.98),_rgba(5,8,22,1))]" />
        <div className="relative max-w-6xl mx-auto px-6 py-6 sm:px-8 lg:px-10">
          <div className="flex items-center justify-between gap-4">
            <div>
              <p className="text-xs uppercase tracking-[0.4em] text-sky-300">Jamie's super good media</p>
              <p className="mt-2 text-sm text-slate-400">Real estate media concept page</p>
            </div>
            <a
              href="#contact"
              className="inline-flex items-center justify-center rounded-full bg-sky-400 px-5 py-3 text-sm font-semibold text-slate-950 shadow-[0_10px_30px_rgba(56,189,248,0.35)] transition hover:bg-sky-300"
            >
              Book now!
            </a>
          </div>

          <div className="grid gap-12 py-16 lg:grid-cols-[1.1fr_0.9fr] lg:items-center">
            <div className="space-y-8">
              <div className="inline-flex items-center rounded-full border border-slate-800 bg-slate-900/70 px-4 py-2 text-sm text-slate-300 backdrop-blur">
                Premium photo, video, drone, and launch-day marketing support
              </div>
              <div className="space-y-6">
                <h1 className="max-w-3xl text-5xl font-semibold tracking-tight text-white sm:text-6xl">
                  Media that makes every listing feel like the one buyers have to see first.
                </h1>
                <p className="max-w-2xl text-lg leading-8 text-slate-300">
                  Jamie&apos;s super good media is a polished, dark-theme concept for a real estate media company offering
                  cinematic home coverage, agent-ready edits, and drone footage for homes or commercial properties that are for sale.
                </p>
              </div>

              <div className="grid gap-4 sm:grid-cols-3">
                <div className="rounded-3xl border border-slate-800 bg-white/5 p-5 backdrop-blur">
                  <p className="text-3xl font-semibold text-white">24h</p>
                  <p className="mt-2 text-sm text-slate-400">Typical turnaround for launch-ready media.</p>
                </div>
                <div className="rounded-3xl border border-slate-800 bg-white/5 p-5 backdrop-blur">
                  <p className="text-3xl font-semibold text-white">4K</p>
                  <p className="mt-2 text-sm text-slate-400">Clean video capture for detail-rich property showcases.</p>
                </div>
                <div className="rounded-3xl border border-slate-800 bg-white/5 p-5 backdrop-blur">
                  <p className="text-3xl font-semibold text-white">MLS+</p>
                  <p className="mt-2 text-sm text-slate-400">Assets sized for listing sites, reels, and email campaigns.</p>
                </div>
              </div>
            </div>

            <div className="rounded-[2rem] border border-slate-800 bg-slate-950/70 p-4 shadow-[0_20px_80px_rgba(15,23,42,0.65)] backdrop-blur">
              <HeroArtwork />
            </div>
          </div>
        </div>
      </section>

      <section className="border-y border-slate-900 bg-slate-950/80">
        <div className="max-w-6xl mx-auto px-6 py-18 sm:px-8 lg:px-10">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
            <div className="space-y-3">
              <p className="text-sm font-medium uppercase tracking-[0.35em] text-sky-300">Media, your way</p>
              <h2 className="text-3xl font-semibold text-white sm:text-4xl">Built for the way agents, teams, and brokers actually market properties.</h2>
            </div>
            <p className="max-w-2xl text-base leading-7 text-slate-400">
              Pick a single service or a complete launch package. The concept is simple: premium visuals, easy booking, and assets ready to use the moment a listing goes live.
            </p>
          </div>

          <div className="mt-10 grid gap-6 lg:grid-cols-3">
            {services.map((service) => (
              <div key={service.title} className="rounded-[1.75rem] border border-slate-800 bg-white/[0.04] p-7 shadow-[0_10px_40px_rgba(2,6,23,0.55)]">
                <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-sky-400/10 text-sky-300">
                  ✦
                </div>
                <h3 className="mt-6 text-xl font-semibold text-white">{service.title}</h3>
                <p className="mt-3 text-sm leading-7 text-slate-400">{service.description}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="max-w-6xl mx-auto px-6 py-18 sm:px-8 lg:px-10">
        <div className="grid gap-6 lg:grid-cols-[1fr_1.1fr]">
          <div className="rounded-[2rem] border border-slate-800 bg-gradient-to-br from-slate-900 to-slate-950 p-8">
            <p className="text-sm font-medium uppercase tracking-[0.35em] text-sky-300">About us</p>
            <h2 className="mt-4 text-3xl font-semibold text-white">Professional, calm, and built to make properties look their best.</h2>
            <p className="mt-5 text-base leading-8 text-slate-300">
              Jamie&apos;s super good media is positioned as a modern partner for agents who want beautiful visuals without a complicated process. The brand voice is premium, friendly, and fast-moving.
            </p>
            <div className="mt-8 rounded-[1.5rem] border border-slate-800 bg-white/[0.03] p-6">
              <p className="text-sm font-medium text-white">What this concept emphasizes</p>
              <ul className="mt-4 space-y-3 text-sm text-slate-400">
                {workflow.map((item) => (
                  <li key={item} className="flex items-start gap-3">
                    <span className="mt-1 text-sky-300">•</span>
                    <span>{item}</span>
                  </li>
                ))}
              </ul>
            </div>
          </div>

          <div className="rounded-[2rem] border border-slate-800 bg-white/[0.04] p-8">
            <p className="text-sm font-medium uppercase tracking-[0.35em] text-sky-300">Contact</p>
            <h2 id="contact" className="mt-4 text-3xl font-semibold text-white">Ready to book the next listing?</h2>
            <p className="mt-5 text-base leading-8 text-slate-300">
              This proof of concept keeps the interaction simple for now: a clear CTA, straightforward contact options, and a shareable preview URL for review.
            </p>

            <div className="mt-8 grid gap-4 sm:grid-cols-2">
              <a href="mailto:hello@jsgmedia.example" className="rounded-[1.5rem] border border-slate-800 bg-slate-950/70 p-5 transition hover:border-sky-400/60">
                <p className="text-sm text-slate-400">Email</p>
                <p className="mt-2 text-lg font-semibold text-white">hello@jsgmedia.example</p>
              </a>
              <a href="tel:+15550100101" className="rounded-[1.5rem] border border-slate-800 bg-slate-950/70 p-5 transition hover:border-sky-400/60">
                <p className="text-sm text-slate-400">Phone</p>
                <p className="mt-2 text-lg font-semibold text-white">(555) 010-0101</p>
              </a>
            </div>

            <div className="mt-6 rounded-[1.5rem] border border-slate-800 bg-sky-400/10 p-5">
              <p className="text-sm font-medium text-sky-300">Shareable Azure URL</p>
              <a href={shareUrl} className="mt-3 block break-all text-sm text-white underline decoration-sky-400/50 underline-offset-4 hover:text-sky-200">
                {shareUrl}
              </a>
              <p className="mt-3 text-sm text-slate-300">
                This route will be available after the app deploys from <span className="font-semibold text-white">develop</span>.
              </p>
            </div>
          </div>
        </div>

        <div className="mt-8 flex flex-col gap-4 rounded-[2rem] border border-slate-800 bg-white/[0.03] px-6 py-5 text-sm text-slate-400 sm:flex-row sm:items-center sm:justify-between">
          <span>Proof of concept for a real estate media company website.</span>
          <Link to="/" className="font-medium text-sky-300 transition hover:text-sky-200">
            Back to HardwareStore
          </Link>
        </div>
      </section>
    </div>
  );
}
