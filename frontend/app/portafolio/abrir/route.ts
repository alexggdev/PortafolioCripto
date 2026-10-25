import { NextRequest, NextResponse } from "next/server";
import { SELECTED_COOKIE, VIEW_COOKIE } from "@/features/portfolio/lib/selectedPortfolio";

/** «Mi portafolio» de la barra superior: olvida la selección anterior y vuelve a /portafolio, que abre el predeterminado. */
export function GET(request: NextRequest) {
  const response = NextResponse.redirect(new URL("/portafolio", request.nextUrl));
  response.cookies.delete(SELECTED_COOKIE);
  response.cookies.delete(VIEW_COOKIE);
  return response;
}
