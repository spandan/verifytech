import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { DashboardHeader } from "@/components/DashboardHeader";
import "./globals.css";

const geistSans = Geist({ variable: "--font-geist-sans", subsets: ["latin"] });
const geistMono = Geist_Mono({ variable: "--font-geist-mono", subsets: ["latin"] });

export const metadata: Metadata = {
  title: "DevicePassport Dashboard",
  description: "Manage your certified devices",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className={`${geistSans.variable} ${geistMono.variable} min-h-screen flex flex-col`}>
        <DashboardHeader />
        <main className="flex-1">{children}</main>
      </body>
    </html>
  );
}
