import { useEffect, useRef, useState } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import logo from '../assets/LogoWrap.png';
import { useMe } from '../api/queries';
import { logout } from '../auth/session';

const NAVIGATION_ITEMS = [
  {
    id: "dashboard",
    to: "/",
    label: "Dashboard",
    icon: (
      <svg xmlns="http://www.w3.org/2000/svg" className="size-[18px] fill-current overflow-visible" viewBox="0 0 512 512" aria-hidden="true">
        <path d="M426 495.983H86c-25.364 0-46-20.635-46-46v-242.02c0-8.836 7.163-16 16-16s16 7.164 16 16v242.02c0 7.72 6.28 14 14 14h340c7.72 0 14-6.28 14-14v-242.02c0-8.836 7.163-16 16-16s16 7.164 16 16v242.02c0 25.364-20.635 46-46 46" />
        <path d="M496 263.958a15.95 15.95 0 0 1-11.313-4.687L285.698 60.284c-16.375-16.376-43.02-16.376-59.396 0L27.314 259.272c-6.248 6.249-16.379 6.249-22.627 0-6.249-6.248-6.249-16.379 0-22.627L203.675 37.656c28.852-28.852 75.799-28.852 104.65 0l198.988 198.988c6.249 6.249 6.249 16.379 0 22.627A15.94 15.94 0 0 1 496 263.958M320 495.983H192c-8.837 0-16-7.164-16-16v-142c0-27.57 22.43-50 50-50h60c27.57 0 50 22.43 50 50v142c0 8.836-7.163 16-16 16m-112-32h96v-126c0-9.925-8.075-18-18-18h-60c-9.925 0-18 8.075-18 18z" />
      </svg>
    ),
  },
  {
    id: "stokvel",
    to: "/stokvels",
    label: "Stokvel",
    icon: (
      <svg xmlns="http://www.w3.org/2000/svg" className="size-[18px] fill-current overflow-visible" viewBox="0 0 507.246 507.246" aria-hidden="true">
        <path d="M457.262 89.821c-2.734-35.285-32.298-63.165-68.271-63.165H68.5c-37.771 0-68.5 30.729-68.5 68.5V412.09c0 37.771 30.729 68.5 68.5 68.5h370.247c37.771 0 68.5-30.729 68.5-68.5V155.757c-.001-31.354-21.184-57.836-49.985-65.936M68.5 58.656h320.492c17.414 0 32.008 12.261 35.629 28.602H68.5c-13.411 0-25.924 3.889-36.5 10.577v-2.679c0-20.126 16.374-36.5 36.5-36.5M438.746 448.59H68.5c-20.126 0-36.5-16.374-36.5-36.5V155.757c0-20.126 16.374-36.5 36.5-36.5h370.247c20.126 0 36.5 16.374 36.5 36.5v55.838H373.221c-40.43 0-73.322 32.893-73.322 73.323s32.893 73.323 73.322 73.323h102.025v53.849c0 20.126-16.374 36.5-36.5 36.5m36.5-122.349H373.221c-22.785 0-41.322-18.537-41.322-41.323s18.537-41.323 41.322-41.323h102.025z" />
        <circle cx="379.16" cy="286.132" r="16.658" />
      </svg>
    ),
  },
  {
    id: "contributions",
    to: "/contributions",
    label: "Contributions",
    icon: (
      <svg xmlns="http://www.w3.org/2000/svg" className="size-[18px] fill-current overflow-visible" viewBox="0 0 28 28" aria-hidden="true">
        <g transform="translate(-1.68 -1.68)scale(1.12)">
          <path d="M20 26H8c-3.3 0-6-2.7-6-6V8c0-3.3 2.7-6 6-6h12c3.3 0 6 2.7 6 6v12c0 3.3-2.7 6-6 6M8 4C5.8 4 4 5.8 4 8v12c0 2.2 1.8 4 4 4h12c2.2 0 4-1.8 4-4V8c0-2.2-1.8-4-4-4zm6 16c-.6 0-1-.4-1-1v-4H9c-.6 0-1-.4-1-1s.4-1 1-1h4V9c0-.6.4-1 1-1s1 .4 1 1v4h4c.6 0 1 .4 1 1s-.4 1-1 1h-4v4c0 .6-.4 1-1 1" />
        </g>
      </svg>
    ),
  },
  {
    id: "payouts",
    label: "Payouts",
    icon: (
      <svg xmlns="http://www.w3.org/2000/svg" className="size-[18px] fill-current overflow-visible" viewBox="0 0 64 64" aria-hidden="true">
        <path fillRule="evenodd" d="M14.6 6.688c-7.29 0-13.2 5.862-13.2 13.092V41.6c0 7.23 5.91 13.092 13.2 13.092h24.2a2.2 2.182 0 1 0 0-4.364H14.6A8.8 8.728 0 0 1 5.8 41.6V24.144h52.8v7.637a2.2 2.182 0 1 0 4.4 0V19.78c0-7.23-5.91-13.092-13.2-13.092zm44 13.092H5.8a8.8 8.728 0 0 1 8.8-8.728h35.2a8.8 8.728 0 0 1 8.8 8.728" clipRule="evenodd" />
        <path d="M63 45.964a8.8 8.728 0 0 0-8.8-8.728H38.8a2.2 2.182 0 1 0 0 4.364h15.4a4.4 4.364 0 0 1 0 8.728h-1.84l.645-.64a2.2 2.182 0 1 0-3.11-3.085l-4.4 4.364a2.2 2.182 0 0 0 0 3.085l4.4 4.364a2.2 2.182 0 1 0 3.11-3.085l-.644-.64H54.2a8.8 8.728 0 0 0 8.8-8.727M12.4 28.508a2.2 2.182 0 1 0 0 4.364h8.8a2.2 2.182 0 1 0 0-4.364z" />
      </svg>
    ),
  },
  {
    id: "members",
    label: "Members",
    icon: (
      <svg xmlns="http://www.w3.org/2000/svg" className="size-[18px] fill-current overflow-visible" viewBox="0 0 512 512" aria-hidden="true">
        <path d="M253.414 103.434c48.556 0 87.919 40.52 87.919 90.505s-39.363 90.505-87.919 90.505-87.919-40.521-87.919-90.505 39.363-90.505 87.919-90.505m0 36.202c-28.324 0-51.717 24.081-51.717 54.303s23.393 54.303 51.717 54.303 51.717-24.081 51.717-54.303-23.393-54.303-51.717-54.303" />
        <path d="M253.414 0c139.957 0 253.414 113.457 253.414 253.414 0 94.285-51.491 176.544-127.886 220.19-35.728 20.575-77.036 32.582-121.104 33.199l-4.423.025C113.457 506.828 0 393.371 0 253.414S113.457 0 253.414 0m-23.676 346.505c-46.331 0-87.479 29.378-102.607 73.008l-2.339 7.571c35.919 27.232 80.165 42.893 126.504 43.522l5.709-.009c38.24-.62 74.079-11.122 105.072-29.064l19.977-13.243-2.237-6.866c-14.371-44.046-55.062-74.052-101.239-74.901zm23.676-310.303c-119.963 0-217.212 97.249-217.212 217.212 0 57.493 22.337 109.77 58.807 148.624 21.668-55.072 74.965-91.735 134.73-91.735h46.831c59.905 0 113.311 36.835 134.885 92.121 36.686-38.892 59.172-91.325 59.172-149.01-.001-119.963-97.25-217.212-217.213-217.212" />
      </svg>
    ),
  },
  {
    id: "settings",
    label: "Settings",
    icon: (
      <svg xmlns="http://www.w3.org/2000/svg" className="size-[18px] fill-current overflow-visible" viewBox="0 0 32 32" aria-hidden="true">
        <g data-name="Layer 2">
          <path d="M24.915 3.663a3.15 3.15 0 0 0-2.688-1.554H9.774a3.15 3.15 0 0 0-2.688 1.554L.859 14.446a3.15 3.15 0 0 0 0 3.15l6.227 10.742a3.15 3.15 0 0 0 2.688 1.554h12.453a3.15 3.15 0 0 0 2.688-1.554l6.226-10.784a3.15 3.15 0 0 0 0-3.15zm4.41 12.841-6.227 10.784a1.05 1.05 0 0 1-.871.504H9.774a1.05 1.05 0 0 1-.872-.504L2.676 16.504a1.05 1.05 0 0 1 0-1.05L8.902 4.713a1.05 1.05 0 0 1 .872-.504h12.453a1.05 1.05 0 0 1 .871.504l6.227 10.783a1.05 1.05 0 0 1 0 1.008" />
          <path d="M16 9.7a6.3 6.3 0 1 0 6.3 6.3A6.3 6.3 0 0 0 16 9.7m0 10.5a4.2 4.2 0 1 1 4.2-4.2 4.2 4.2 0 0 1-4.2 4.2" />
        </g>
      </svg>
    ),
  },
];

const PROFILE_MENU = [
  { label: "My Profile", icon: <svg className="size-[18px] fill-current" aria-hidden="true" viewBox="0 0 512 512"><path d="M253.414 103.434c48.556 0 87.919 40.52 87.919 90.505s-39.363 90.505-87.919 90.505-87.919-40.521-87.919-90.505 39.363-90.505 87.919-90.505m0 36.202c-28.324 0-51.717 24.081-51.717 54.303s23.393 54.303 51.717 54.303 51.717-24.081 51.717-54.303-23.393-54.303-51.717-54.303" /><path d="M253.414 0c139.957 0 253.414 113.457 253.414 253.414 0 94.285-51.491 176.544-127.886 220.19-35.728 20.575-77.036 32.582-121.104 33.199l-4.423.025C113.457 506.828 0 393.371 0 253.414S113.457 0 253.414 0m-23.676 346.505c-46.331 0-87.479 29.378-102.607 73.008l-2.339 7.571c35.919 27.232 80.165 42.893 126.504 43.522l5.709-.009c38.24-.62 74.079-11.122 105.072-29.064l19.977-13.243-2.237-6.866c-14.371-44.046-55.062-74.052-101.239-74.901zm23.676-310.303c-119.963 0-217.212 97.249-217.212 217.212 0 57.493 22.337 109.77 58.807 148.624 21.668-55.072 74.965-91.735 134.73-91.735h46.831c59.905 0 113.311 36.835 134.885 92.121 36.686-38.892 59.172-91.325 59.172-149.01-.001-119.963-97.25-217.212-217.213-217.212" /></svg> },
  { label: "Account Settings", icon: <svg className="size-[18px] fill-current" aria-hidden="true" viewBox="0 0 32 32"><g><path d="M24.915 3.663a3.15 3.15 0 0 0-2.688-1.554H9.774a3.15 3.15 0 0 0-2.688 1.554L.859 14.446a3.15 3.15 0 0 0 0 3.15l6.227 10.742a3.15 3.15 0 0 0 2.688 1.554h12.453a3.15 3.15 0 0 0 2.688-1.554l6.226-10.784a3.15 3.15 0 0 0 0-3.15zm4.41 12.841-6.227 10.784a1.05 1.05 0 0 1-.871.504H9.774a1.05 1.05 0 0 1-.872-.504L2.676 16.504a1.05 1.05 0 0 1 0-1.05L8.902 4.713a1.05 1.05 0 0 1 .872-.504h12.453a1.05 1.05 0 0 1 .871.504l6.227 10.783a1.05 1.05 0 0 1 0 1.008" /><path d="M16 9.7a6.3 6.3 0 1 0 6.3 6.3A6.3 6.3 0 0 0 16 9.7m0 10.5a4.2 4.2 0 1 1 4.2-4.2 4.2 4.2 0 0 1-4.2 4.2" /></g></svg> },
  { label: "Billing & Payments", icon: <svg className="size-[18px] fill-current" aria-hidden="true" viewBox="0 0 512 512"><path d="M456 80H56c-30.878 0-56 25.122-56 56v240c0 30.878 25.122 56 56 56h400c30.878 0 56-25.122 56-56V136c0-30.878-25.122-56-56-56M56 112h400c13.233 0 24 10.767 24 24v32H32v-32c0-13.233 10.767-24 24-24m400 288H56c-13.233 0-24-10.767-24-24V200h448v176c0 13.233-10.767 24-24 24" /><path d="M112 352H96c-8.836 0-16-7.164-16-16v-16c0-8.836 7.164-16 16-16h16c8.836 0 16 7.164 16 16v16c0 8.836-7.164 16-16 16" /></svg> },
  { label: "Logout", icon: <svg className="size-[18px] fill-current" aria-hidden="true" viewBox="0 0 6.35 6.35"><path d="M3.172.292a.289.29 0 0 0-.286.292v2.318a.289.29 0 0 0 .578 0V.584a.289.29 0 0 0-.292-.292m1.683.58a.289.29 0 0 0-.029 0 .289.29 0 0 0-.16.512c.5.426.816 1.06.816 1.772A2.31 2.31 0 0 1 3.176 5.48 2.31 2.31 0 0 1 .87 3.16c0-.709.311-1.339.806-1.766a.289.29 0 1 0-.375-.44 2.9 2.9 0 0 0-1.01 2.203A2.9 2.9 0 0 0 3.178 6.06 2.896 2.896 0 0 0 6.06 3.156 2.9 2.9 0 0 0 5.04.944a.289.29 0 0 0-.185-.072" /></svg> },
];

/** "Lerato Mokoena" -> "LM". Empty until the profile has loaded. */
function initials(fullName: string | undefined): string {
  if (!fullName) return "";
  return fullName.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0].toUpperCase()).join("");
}

export default function AppLayout() {
  // Mobile first: the sidebar starts closed (a drawer over the page on a phone).
  // From the lg breakpoint up it is always shown, so this only matters on small screens.
  const [isSidebarOpen, setSidebarOpen] = useState(false);
  const [isProfileOpen, setProfileOpen] = useState(false);

  const meQuery = useMe();
  const { pathname } = useLocation();
  const title = NAVIGATION_ITEMS.find((item) => item.to === pathname)?.label ?? "Dashboard";

  // The ref points at the profile <div>, so we can tell whether a click landed inside it.
  const profileRef = useRef<HTMLDivElement>(null);

  // Handle outside clicks
  useEffect(() => {
    // document.addEventListener hands us the browser's own event types, not React's.
    const handleClickOutside = (event: MouseEvent) => {
      // event.target is typed as "maybe null", so we say it is a DOM Node for contains().
      if (profileRef.current && !profileRef.current.contains(event.target as Node)) setProfileOpen(false);
    };
    const handleEsc = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setProfileOpen(false);
        setSidebarOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("keydown", handleEsc);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleEsc);
    };
  }, []);

  
  return (
    // The page content goes in <main> below, where <Outlet /> renders the current route.
    <div className="min-h-screen">
      {/* Dark backdrop behind the open drawer on phones. Tapping it closes the drawer. */}
      {isSidebarOpen && (
        <div className="fixed inset-0 z-30 bg-black/50 lg:hidden" onClick={() => setSidebarOpen(false)} aria-hidden="true" />
      )}

      {/* Sidebar: slid off-screen by default (phones), always in place from lg up. */}
      <aside
        id="sidebar"
        className={`fixed inset-y-0 left-0 z-40 w-64 transition-transform duration-300 ease-in-out lg:translate-x-0 ${isSidebarOpen ? "translate-x-0" : "-translate-x-full"}`}
        aria-label="Sidebar navigation"
      >
        <div className="flex h-full flex-col overflow-auto py-6 px-4 bg-[#0F3D32] text-white border-r border-white/10">
            <div className="mb-6">
              <a href="#" className="min-h-9 inline-block focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 rounded">
                <span className="sr-only">TalliVel</span>
                {/* alt is empty because the sr-only text above already names the link */}
                <img src={logo} alt="" className="w-36 block" />
              </a>

              {/* Search Box */}
              <form role="search" id="searchBox"
                className="flex items-center gap-2.5 mt-4 px-3 py-2.5 rounded-md bg-white/10 outline-1 -outline-offset-1 outline-white/20 focus-within:outline-2 focus-within:-outline-offset-2 focus-within:outline-white">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 192.904 192.904" className="size-4 fill-white/70"
                  aria-hidden="true">
                  <path
                    d="m190.707 180.101-47.078-47.077c11.702-14.072 18.752-32.142 18.752-51.831C162.381 36.423 125.959 0 81.191 0 36.422 0 0 36.423 0 81.193c0 44.767 36.422 81.187 81.191 81.187 19.688 0 37.759-7.049 51.831-18.751l47.079 47.078a7.474 7.474 0 0 0 5.303 2.197 7.498 7.498 0 0 0 5.303-12.803zM15 81.193C15 44.694 44.693 15 81.191 15c36.497 0 66.189 29.694 66.189 66.193 0 36.496-29.692 66.187-66.189 66.187C44.693 147.38 15 117.689 15 81.193z">
                  </path>
                </svg>
                <input type="search" placeholder="Search..."
                  className="text-sm text-white placeholder:text-white/60 w-full outline-none bg-transparent" />
              </form>
            </div>

            <nav className="flex-1" aria-label="Primary sidebar navigation">
              <ul className="space-y-2 text-sm text-white font-medium">
                {NAVIGATION_ITEMS.map((item) => (
                  <li key={item.id}>
                    {item.to ? (
                      <NavLink
                        to={item.to}
                        end={item.to === "/"}
                        onClick={() => setSidebarOpen(false)}
                        className={({ isActive }) =>
                          `flex items-center gap-2.5 hover:bg-white/10 rounded-md px-3 py-2 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-white ${isActive ? "bg-white/15" : ""}`
                        }
                      >
                        {item.icon}
                        <span>{item.label}</span>
                      </NavLink>
                    ) : (
                      // No page behind this one yet.
                      <a
                        href="#"
                        onClick={() => setSidebarOpen(false)}
                        className="flex items-center gap-2.5 hover:bg-white/10 rounded-md px-3 py-2 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-white"
                      >
                        {item.icon}
                        <span>{item.label}</span>
                      </a>
                    )}
                  </li>
                ))}
              </ul>
            </nav>

            <a href="#" className="flex flex-wrap items-center gap-4 rounded-md mt-6 focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-500">
              <span
                aria-hidden="true"
                className="flex size-10 items-center justify-center rounded-md border border-white/20 bg-white/10 text-sm font-semibold text-white"
              >
                {initials(meQuery.data?.fullName)}
              </span>
              <div>
                <p className="text-sm text-white font-medium">{meQuery.data?.fullName ?? "…"}</p>
                <p className="text-xs text-white/70 mt-0.5 break-all">{meQuery.data?.email ?? ""}</p>
              </div>
            </a>
          </div>
        </aside>

      {/* Main content: full width on a phone, pushed right of the sidebar from lg up. */}
      <div className="lg:pl-64">
          <header className="flex py-2 sticky top-0 w-full bg-[#0F3D32] text-white border-b border-white/10 px-4 sm:px-6 min-h-[68px] z-20">
            <div className="flex flex-wrap items-center gap-4 w-full">
              <button
                type="button"
                onClick={() => setSidebarOpen(!isSidebarOpen)}
                aria-expanded={isSidebarOpen}
                aria-controls="sidebar"
                className="cursor-pointer rounded focus:outline-none focus-visible:ring-2 focus-visible:ring-white lg:hidden"
              >
                <span className="sr-only">Toggle sidebar menu</span>
                <svg xmlns="http://www.w3.org/2000/svg" className="size-[18px] fill-white" viewBox="0 0 20 20" aria-hidden="true">
                  <path fillRule="evenodd"
                    d="M.13 17.05a1.41 1.41 0 0 1 1.41-1.41H10a1.41 1.41 0 1 1 0 2.82H1.54a1.41 1.41 0 0 1-1.41-1.41zm0-14.1a1.41 1.41 0 0 1 1.41-1.41h16.92a1.41 1.41 0 1 1 0 2.82H1.54A1.41 1.41 0 0 1 .13 2.95zm0 7.05a1.41 1.41 0 0 1 1.41-1.41h16.92a1.41 1.41 0 1 1 0 2.82H1.54A1.41 1.41 0 0 1 .13 10z"
                    clipRule="evenodd" data-original="#000000" />
                </svg>
              </button>

              <h1 className="text-xl text-white font-bold">{title}</h1>

              <div className="flex items-center flex-wrap gap-5 ml-auto">
                {/* Notification Bell */}
                <a href="#"
                  className="relative block focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 rounded">
                  <span className="sr-only">View notifications</span>
                  <svg xmlns="http://www.w3.org/2000/svg"
                    className="size-5 fill-white overflow-visible"
                    viewBox="0 0 371.263 371.263" aria-hidden="true">
                    <path
                      d="M305.402 234.794v-70.54c0-52.396-33.533-98.085-79.702-115.151.539-2.695.838-5.449.838-8.204C226.539 18.324 208.215 0 185.64 0s-40.899 18.324-40.899 40.899c0 2.695.299 5.389.778 7.964-15.868 5.629-30.539 14.551-43.054 26.647-23.593 22.755-36.587 53.354-36.587 86.169v73.115c0 2.575-2.096 4.731-4.731 4.731-22.096 0-40.959 16.647-42.995 37.845-1.138 11.797 2.755 23.533 10.719 32.276 7.904 8.683 19.222 13.713 31.018 13.713h72.217c2.994 26.887 25.869 47.905 53.534 47.905s50.54-21.018 53.534-47.905h72.217c11.797 0 23.114-5.03 31.018-13.713 7.904-8.743 11.797-20.479 10.719-32.276-2.036-21.198-20.958-37.845-42.995-37.845a4.704 4.704 0 0 1-4.731-4.731zM185.64 23.952c9.341 0 16.946 7.605 16.946 16.946 0 .778-.12 1.497-.24 2.275-4.072-.599-8.204-1.018-12.336-1.138-7.126-.24-14.132.24-21.078 1.198-.12-.778-.24-1.497-.24-2.275.002-9.401 7.607-17.006 16.948-17.006zm0 323.358c-14.431 0-26.527-10.3-29.342-23.952h58.683c-2.813 13.653-14.909 23.952-29.341 23.952zm143.655-67.665c.479 5.15-1.138 10.12-4.551 13.892-3.533 3.773-8.204 5.868-13.353 5.868H59.89c-5.15 0-9.82-2.096-13.294-5.868-3.473-3.772-5.09-8.743-4.611-13.892.838-9.042 9.282-16.168 19.162-16.168 15.809 0 28.683-12.874 28.683-28.683v-73.115c0-26.228 10.419-50.719 29.282-68.923 18.024-17.425 41.498-26.887 66.528-26.887 1.198 0 2.335 0 3.533.06 50.839 1.796 92.277 45.929 92.277 98.325v70.54c0 15.809 12.874 28.683 28.683 28.683 9.88 0 18.264 7.126 19.162 16.168z"
                      data-original="#000000"></path>
                  </svg>
                  <span className="absolute top-0 right-0 size-2.5 bg-red-500 rounded-full"></span>
                </a>

                {/* Profile Dropdown */}
                <div className="relative w-max flex flex-col" ref={profileRef}>
                

                  <ul
                    className={`${isProfileOpen ? "block" : "hidden"
                      } absolute right-0 top-full mt-2 p-2 space-y-0.5 min-w-48 w-full text-slate-800 text-sm font-medium bg-white border border-slate-300 rounded-md shadow-lg z-20 overflow-hidden dark:text-slate-400 dark:bg-neutral-800 dark:border-neutral-700`}
                  >
                    {PROFILE_MENU.map((item) => (
                      <li key={item.label}>
                        <a
                          href="#"
                          onClick={(e) => {
                            setProfileOpen(false);
                            if (item.label === "Logout") {
                              e.preventDefault();
                              void logout();
                            }
                          }}
                          className="w-full p-2 flex items-center gap-2.5 rounded-md cursor-pointer transition-colors hover:text-slate-900 hover:bg-slate-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:hover:text-slate-50 dark:hover:bg-neutral-700"
                        >
                          {item.icon}
                          {item.label}
                        </a>
                      </li>
                    ))}
                  </ul>
                </div>
              </div>
            </div>
          </header>

          <main className="p-4 sm:p-6">
            <Outlet />
          </main>
      </div>
    </div>
  );
}
