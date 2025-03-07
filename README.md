# mintc
mintc is a compiler for MPL, a Forth-inspired stack-based language designed for writing small programs, targeting the [Mint virtual machine](https://github.com/solarnomad7/mint).

## About MPL
While a stack-based paradigm can take some getting used to, MPL syntax is very simple. Code consists of one or more *array* definitions containing *literals* and *words*. Take a look at some examples:

```
#import io.mo

main:
  "Hello, world!\n" print
end
```

```
#import array.mo
#import io.mo
#import string.mo

my_numbers: [1 2 3 4 5];
main:
  @my_numbers sum
  int>str print
end
```

mintc comes with a compact standard library that implements common functions, such as input and output.

## Usage
- Compile an executable: `mintc -cx [source] [destination]`
- Compile an object: `mintc -co [source] [destination]`
