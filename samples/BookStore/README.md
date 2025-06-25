English | [繁體中文](README.zh.md)

# BookStore ServiceFramework Sample

This sample demonstrates how to use EdgeSync ServiceFramework with Clean Architecture pattern, using NATS messaging instead of traditional HTTP APIs.

## Architecture Overview

The solution follows Clean Architecture principles with the following layers:

- **Domain**: Core business entities, specifications, and interfaces
- **Application**: Business logic, DTOs, and application services
- **Infrastructure**: Data access implementation (InMemoryRepository)
- **NATS API**: NATS-based service handlers
- **NATS Client**: Client library for consuming NATS services
- **Web API**: HTTP API that demonstrates NATS client usage

## Features

- ✅ Clean Architecture implementation
- ✅ ErrorOr pattern for error handling
- ✅ NATS messaging with ServiceFramework
- ✅ AutoMapper for object mapping
- ✅ JetBrains.Annotations for validation
- ✅ Domain specifications for business rules
- ✅ In-memory repository for data storage
- ✅ Swagger/OpenAPI documentation

## Project Structure

```
BookStore/
├── BookStore.Domain/           # Domain entities, specifications, errors
├── BookStore.Application/      # Application services, DTOs, mappings
├── BookStore.Infrastructure/   # InMemoryRepository implementation
├── BookStore.Nats.Api/        # NATS ServiceHandlers
├── BookStore.Nats.Client/     # NATS client library
└── BookStore.Web.Api/         # HTTP API demo
```

## Prerequisites

- .NET 9.0 SDK
- NATS Server

## Getting Started

### Step 1: Install and Start NATS Server

#### Option A: Using Docker (Recommended)
```bash
docker run -d --name nats-server -p 4222:4222 -p 8222:8222 nats:latest
```

#### Option B: Install NATS Server Locally
1. Download from: https://docs.nats.io/running-a-nats-service/introduction/installation
2. Start with: `nats-server`

### Step 2: Build the Solution
```bash
cd samples/BookStore
dotnet build
```

### Step 3: Start the NATS API Service
```bash
cd BookStore.Nats.Api
dotnet run
```

### Step 4: Start the Web API (New Terminal)
```bash
cd BookStore.Web.Api
dotnet run
```

### Step 5: Test the API

#### Access Swagger UI
Open your browser and navigate to: `https://localhost:xxxx/swagger` (replace xxxx with actual port)

## Testing Methods

### Method 1: Swagger UI (Easiest)
1. Open `https://localhost:xxxx/swagger`
2. Try the following operations:
   - **GET /api/books** - Get all books (should return 3 sample books)
   - **GET /api/books/{id}** - Get a specific book (copy an ID from the list)
   - **POST /api/books** - Create a new book
   - **PUT /api/books/{id}** - Update a book
   - **DELETE /api/books/{id}** - Delete a book

### Method 2: curl Commands

#### Get All Books
```bash
curl -X GET "https://localhost:xxxx/api/books" -k
```

#### Get Specific Book
```bash
# First get all books to find an ID, then:
curl -X GET "https://localhost:xxxx/api/books/{book-id}" -k
```

#### Create New Book
```bash
curl -X POST "https://localhost:xxxx/api/books" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Design Patterns",
    "author": "Gang of Four",
    "isbn": "9780201633610",
    "price": 54.99,
    "stock": 15
  }' -k
```

#### Update Book
```bash
curl -X PUT "https://localhost:xxxx/api/books/{book-id}" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Updated Title",
    "author": "Updated Author",
    "price": 59.99,
    "stock": 20
  }' -k
```

#### Delete Book
```bash
curl -X DELETE "https://localhost:xxxx/api/books/{book-id}" -k
```

## Sample Data

The InMemoryRepository includes 3 sample books:

1. **Clean Architecture** by Robert C. Martin
   - ISBN: 9780134494166
   - Price: $45.99
   - Stock: 10

2. **Domain-Driven Design** by Eric Evans
   - ISBN: 9780321125217
   - Price: $52.99
   - Stock: 5

3. **Microservices Patterns** by Chris Richardson
   - ISBN: 9781617294549
   - Price: $48.99
   - Stock: 8

## Configuration

### Environment Variables
- `MSG_BROKER_URL`: NATS broker URL (default: `nats://localhost:4222`)
- `MSG_BUS_URL`: NATS bus URL (default: `nats://localhost:4222`)

### Custom Configuration
You can modify the NATS URLs in `Program.cs` files:

**BookStore.Nats.Api/Program.cs:**
+ **Suggestion**: use `AddServiceFramework`
```csharp
builder.Services.AddServiceFramework(static options =>
{
    options.DefaultConnection = "bus";
    options.AddConnection("bus", Environment.GetEnvironmentVariable("MSG_BUS_URL") ?? "nats://localhost:4223")
        .WithCredFile(Environment.GetEnvironmentVariable("MSG_BUS_CREDFILE") ?? string.Empty)
        .WithSerializerRegistry(NatsDefaultSerializerRegistry.Default);

    options.AddConnection("broker", Environment.GetEnvironmentVariable("MSG_BROKER_URL") ?? "nats://localhost:4222")
        .WithCredFile(Environment.GetEnvironmentVariable("MSG_BROKER_CREDFILE") ?? string.Empty)
        .WithSerializerRegistry(NatsProtobufSerializerRegistry.Default);
});
```

+ **Backward compatible** for now, but may be deprecated in the future.: use `AddNatsApi`
```csharp
builder.Services.AddNatsApi(options =>
{
    options.MsgBrokerUrl = "nats://your-broker:4222";
    options.MsgBusUrl = "nats://your-bus:4222";
});
```

## NATS Subjects

The following NATS subjects are available:

- `bookstore.books.{id}.get` - Get single book
- `bookstore.books.get` - Get all books
- `bookstore.books.post` - Create new book
- `bookstore.books.{id}.put` - Update existing book
- `bookstore.books.{id}.delete` - Delete book

## Error Handling

The sample demonstrates comprehensive error handling using the ErrorOr pattern:

### Domain Errors
- **Book.NotFound** (404) - Book not found
- **Book.InvalidTitle** (400) - Invalid title
- **Book.InvalidAuthor** (400) - Invalid author
- **Book.InvalidISBN** (400) - Invalid ISBN format
- **Book.InvalidPrice** (400) - Invalid price
- **Book.InvalidStock** (400) - Invalid stock

### Communication Errors
- **Communication.ConnectionFailed** (503) - NATS connection failed
- **Communication.RequestTimeout** (503) - Request timeout
- **Communication.InvalidResponse** (503) - Invalid response
- **Communication.ServiceUnavailable** (503) - Service unavailable
- **Communication.SerializationFailed** (503) - Serialization failed

## Troubleshooting

### NATS Server Not Running
**Error:** Connection refused or timeout errors
**Solution:** Make sure NATS server is running on port 4222

### Port Already in Use
**Error:** "Unable to bind to https://localhost:xxxx"
**Solution:** 
- Kill the process: `lsof -ti:xxxx | xargs kill -9`
- Or use different port: `dotnet run --urls="https://localhost:8080"`

### API Returns 503 Service Unavailable
**Error:** All API calls return 503
**Solution:** 
1. Check if NATS API service (BookStore.Nats.Api) is running
2. Verify NATS server is accessible
3. Check logs for connection errors

### Validation Errors
The API validates input using JetBrains.Annotations:
- Title: Required, max 200 characters
- Author: Required, max 100 characters  
- ISBN: Required, must be 10 or 13 digits
- Price: Must be > 0 and < 999999.99
- Stock: Must be >= 0

## API Endpoints

| Method | Endpoint | Description | Request Body |
|--------|----------|-------------|--------------|
| GET | `/api/books` | Get all books | None |
| GET | `/api/books/{id}` | Get book by ID | None |
| POST | `/api/books` | Create new book | CreateBookDto |
| PUT | `/api/books/{id}` | Update book | UpdateBookDto |
| DELETE | `/api/books/{id}` | Delete book | None |

### Request/Response Examples

**CreateBookDto:**
```json
{
  "title": "Book Title",
  "author": "Author Name",
  "isbn": "1234567890",
  "price": 29.99,
  "stock": 100
}
```

**BookDto Response:**
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "title": "Book Title",
  "author": "Author Name",
  "isbn": "1234567890",
  "price": 29.99,
  "stock": 100,
  "createdAt": "2024-01-01T00:00:00.000Z"
}
```

## Key Design Patterns

1. **Clean Architecture**: Separation of concerns with dependency inversion
2. **ErrorOr**: Functional error handling without exceptions
3. **Repository Pattern**: Data access abstraction
4. **Specification Pattern**: Business rule encapsulation
5. **CQRS**: Command/Query separation in application services
6. **Messaging Pattern**: NATS-based communication

## Learning Objectives

This sample teaches:

- How to structure a Clean Architecture solution
- How to use EdgeSync ServiceFramework with NATS
- How to implement the ErrorOr pattern
- How to create NATS-based microservices
- How to build client libraries for NATS services
- How to handle errors gracefully in distributed systems
