import { NextRequest, NextResponse } from "next/server";
import { prisma } from "@/lib/db";

export async function GET(req: NextRequest) {
  const { searchParams } = new URL(req.url);
  const date = searchParams.get("date");

  if (!date) {
    return NextResponse.json({ error: "date param required" }, { status: 400 });
  }

  const start = new Date(`${date}T00:00:00.000Z`);
  const end = new Date(`${date}T23:59:59.999Z`);

  const reviews = await prisma.review.findMany({
    where: { date: { gte: start, lte: end } },
    select: { id: true, reviewer: true, review: true, rating: true, date: true },
    orderBy: { rating: "desc" },
  });

  return NextResponse.json({ reviews, total: reviews.length });
}
