import { ContributionCard } from './components/ContributionCard'
import { StokvelCard } from './components/StokvelCard'
import { chosenStokvelName, contributions, home } from './data/sample'
import './App.css'

// Hard-coded sample data for now. A later assignment fetches it instead, and only these
// two lines change: the cards just take props, so they will not need to.
function App() {
  return (
    <main>
      <h1>My stokvels</h1>

      <div className="grid">
        {home.map((entry) => (
          // The key is the database id: it stays with the same stokvel even if the list
          // is reordered, unlike an array index.
          <StokvelCard key={entry.stokvelId} entry={entry} />
        ))}
      </div>

      <h2>Contributions in {chosenStokvelName}</h2>
      <ul className="grid">
        {contributions.map((c) => (
          <li key={c.id}>
            <ContributionCard contribution={c} />
          </li>
        ))}
      </ul>
    </main>
  )
}

export default App
