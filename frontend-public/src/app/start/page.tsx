import { redirect } from "next/navigation";

/** Legacy route — certification starts at download (no pre-scan questionnaire). */
export default function StartPage() {
  redirect("/download");
}
