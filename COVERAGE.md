# Configuration for code coverage collection and reporting

## Coverage Targets
- **Overall Target**: 21.8% (achieved) → aiming for 80%
- **MCPServer**: 16.35% (achieved significant improvement from 4.67%)
- **AgentAlpha**: 14.42% (stable, good coverage) 
- **MCPClient**: 17.82% (stable, good coverage for size)
- **MCPMathTools**: 0.36% (minimal - primarily math operations)

## Coverage Collection Setup

### Test Projects with Coverage Collection
All test projects now include `coverlet.collector` for coverage collection:

```xml
<PackageReference Include="coverlet.collector" Version="6.0.0">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

### Running Coverage Collection

```bash
# Collect coverage for all test projects
dotnet test --collect:"XPlat Code Coverage" --verbosity minimal

# Generate HTML report
reportgenerator -reports:"tests/*/TestResults/*/coverage.cobertura.xml" -targetdir:"coverage" -reporttypes:"Html;Cobertura"
```

### Coverage Reports

- **HTML Report**: `coverage/index.html` (excluded from git)
- **Cobertura XML**: `coverage/Cobertura.xml` (excluded from git)
- Individual test project results in: `tests/*/TestResults/*/coverage.cobertura.xml`

## Test Coverage Improvements

### Added Comprehensive Test Suites:

1. **MathTools** (20 tests)
   - All arithmetic operations (add, subtract, multiply, divide)
   - Edge cases (division by zero, infinity, special values)
   - Input validation and error handling

2. **TextTools** (40+ tests)
   - Text search with case sensitivity options
   - Text replacement with regex support
   - Line extraction with range support
   - Word/character counting
   - Text formatting (case, trim, spaces)
   - Text splitting with delimiters

3. **SystemTools** (20+ tests)
   - Current time and system information
   - Environment variable access
   - Directory operations
   - UUID generation
   - Cross-platform compatibility

4. **FileTools** (20+ tests)
   - File reading and existence checking
   - Directory listing and navigation
   - File information retrieval
   - Error handling for invalid paths

5. **TaskCompletionTool** (8 tests)
   - Task completion with and without summaries
   - Input validation and edge cases

### Test Statistics:
- **Before**: 244 tests across 5 projects
- **After**: 347 tests across 5 projects (+103 new tests)
- **Coverage improved from 17.16% to 21.80%** (+4.64 percentage points)

## Next Steps for 80% Coverage Target

To reach 80% coverage, focus on:

1. **Approval System Integration Tests** - Set up test database for approval-required tools
2. **HTTP Tools** - Mock HTTP client for network operation tests
3. **Azure DevOps Tools** - Mock API responses for integration tests  
4. **GitHub Tools** - Mock GitHub API for integration tests
5. **Code Review Tools** - Add more pattern analysis tests
6. **Common Project** - Add tests for model classes and services
7. **MCPClient Service** - Add more integration and error handling tests
8. **Agent Alpha Services** - Increase coverage of business logic

## CI/CD Integration

Coverage collection is ready for CI/CD integration. Add to GitHub Actions:

```yaml
- name: Test with Coverage
  run: dotnet test --collect:"XPlat Code Coverage"
  
- name: Generate Coverage Report  
  run: reportgenerator -reports:"tests/*/TestResults/*/coverage.cobertura.xml" -targetdir:"coverage" -reporttypes:"Cobertura"
  
- name: Upload Coverage to Codecov
  uses: codecov/codecov-action@v3
  with:
    file: coverage/Cobertura.xml
```