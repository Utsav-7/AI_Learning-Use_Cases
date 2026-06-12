require("dotenv").config();
const fs = require("fs");
const Papa = require("papaparse");
const { Pool } = require("pg");

const pool = new Pool({
  connectionString: process.env.DATABASE_URL,
  ssl: { rejectUnauthorized: false },
});

const START_DATE = new Date("2025-06-13");
const END_DATE = new Date("2026-06-12");
const MAX_ROWS = 5000;

function randomDate(start, end) {
  const ms =
    start.getTime() + Math.random() * (end.getTime() - start.getTime());
  return new Date(ms);
}

async function createTable(client) {
  await client.query(`
    CREATE TABLE IF NOT EXISTS reviews (
      id         SERIAL PRIMARY KEY,
      reviewer   TEXT    NOT NULL,
      review     TEXT    NOT NULL,
      rating     FLOAT   NOT NULL,
      date       TIMESTAMP NOT NULL,
      created_at TIMESTAMP DEFAULT NOW()
    )
  `);
  console.log("Table ready.");
}

async function seed() {
  const csvPath = "C:\\Users\\Bacancy\\Desktop\\AI Learning\\Restaurant Review Intelligence System\\Data\\Restaurant reviews.csv";

  if (!fs.existsSync(csvPath)) {
    console.error(`CSV not found at: ${csvPath}`);
    process.exit(1);
  }

  const client = await pool.connect();

  try {
    await createTable(client);

    const csv = fs.readFileSync(csvPath, "utf8");
    const { data, errors } = Papa.parse(csv, {
      header: true,
      skipEmptyLines: true,
    });

    if (errors.length > 0) {
      console.warn(`CSV parse warnings: ${errors.length}`);
    }

    console.log(`Total rows in CSV: ${data.length}`);
    console.log(`Max rows to insert: ${MAX_ROWS}`);

    let inserted = 0;
    let skipped = 0;

    for (const row of data) {
      const review = row["Review"]?.trim();
      const reviewer = row["Reviewer"]?.trim() || "Anonymous";
      const ratingRaw = row["Rating"]?.trim();

      // Stop once we hit the 5000 row limit
      if (inserted >= MAX_ROWS) break;

      // Skip if review is null or empty
      if (!review) {
        skipped++;
        continue;
      }

      // Rating must be a valid float
      const rating = parseFloat(ratingRaw);
      if (isNaN(rating)) {
        skipped++;
        continue;
      }

      const date = randomDate(START_DATE, END_DATE);

      await client.query(
        "INSERT INTO reviews (reviewer, review, rating, date) VALUES ($1, $2, $3, $4)",
        [reviewer, review, rating, date]
      );

      inserted++;

      if (inserted % 100 === 0) {
        console.log(`  Inserted ${inserted} rows...`);
      }
    }

    console.log("\n--- Done ---");
    console.log(`Inserted : ${inserted}`);
    console.log(`Skipped  : ${skipped} (null review or invalid rating)`);
  } finally {
    client.release();
    await pool.end();
  }
}

seed().catch((err) => {
  console.error("Seed failed:", err.message);
  process.exit(1);
});
